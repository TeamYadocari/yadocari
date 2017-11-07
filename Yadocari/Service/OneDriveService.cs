#region Copyright
/*
 * Yadocari\Service\OneDriveService.cs
 *
 * Copyright (c) 2017 TeamYadocari
 *
 * You can redistribute it and/or modify it under either the terms of
 * the AGPLv3 or YADOCARI binary code license. See the file COPYING
 * included in the YADOCARI package for more in detail.
 *
 */
#endregion
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Helpers;
using Yadocari.Controllers;
using Yadocari.Models;

namespace Yadocari.Service
{
    public static class OneDriveService
    {
        private const string ApiEndPoint = "https://api.onedrive.com/v1.0";
        public static string ClientId => ManageController.GetConfiguration<string>("ClientId");
        private static string ClientSecret => ManageController.GetConfiguration<string>("ClientSecret");
        private static string ServerUrl => ManageController.GetConfiguration<string>("ServerUrl");

        public class ShareInfo
        {
            public string PermissionId { get; set; }
            public string Url { get; set; }
        }

        public class OwnerInfo
        {
            public string Id { get; set; }
            public string DisplayName { get; set; }
            public long FreeSpace { get; set; }
        }

        public static async Task<string> GetRefreshToken(string code)
        {
            var url = "https://login.live.com/oauth20_token.srf";
            var hc = new HttpClient();

            var param = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {"client_id", ClientId },
                {"client_secret", ClientSecret },
                {"code", code },
                {"grant_type", "authorization_code" },
                {"redirect_uri", $"{ServerUrl}/Manage/AddMicrosoftAccountCallback" }
            });

            var response = await hc.PostAsync(url, param);
            var result = await response.Content.ReadAsStringAsync();
            var dynamicResult = Json.Decode(result);
            return dynamicResult.refresh_token;
        }

        public static async Task<OwnerInfo> GetOwnerInfo(string refleshToken)
        {
            var token = await GetAccessToken(refleshToken);

            var url = ApiEndPoint + "/drive";
            var hc = new HttpClient();
            hc.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

            var response = await hc.GetAsync(url);
            var result = await response.Content.ReadAsStringAsync();
            var dynamicResult = Json.Decode(result);
            return new OwnerInfo
            {
                Id = dynamicResult.owner?.user.id,
                DisplayName = dynamicResult.owner?.user.displayname,
                FreeSpace = dynamicResult.quota?.remaining
            };
        }

        private static async Task<string> GetAccessToken(string refleshToken)
        {
            var url = "https://login.live.com/oauth20_token.srf";
            var hc = new HttpClient();

            var param = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {"client_id", ClientId },
                {"client_secret", ClientSecret },
                {"refresh_token", refleshToken },
                {"grant_type", "refresh_token" }
            });

            var response = await hc.PostAsync(url, param);
            var result = await response.Content.ReadAsStringAsync();
            var dynamicResult = Json.Decode(result);
            return dynamicResult.access_token;
        }

        private static async Task<string> Upload(string token, string filename, Stream file)
        {
            var url = ApiEndPoint + $"/drive/root:/{filename}:/content";
            var hc = new HttpClient();
            hc.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

            var streamContent = new StreamContent(file);

            var response = await hc.PutAsync(url, streamContent);
            var result = await response.Content.ReadAsStringAsync();
            var dynamicResult = Json.Decode(result);
            return dynamicResult.id;
        }

        //Uploadするファイルが100MBより大きい
        private static async Task<string> UploadLargeFile(string token, string filename, Stream file)
        {
            var url = ApiEndPoint + $"/drive/root:/{filename}:/upload.createSession";
            var hc = new HttpClient();
            hc.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

            var response = await hc.PostAsync(url, null);
            var result = await response.Content.ReadAsStringAsync();
            var uploadUrl = Json.Decode(result).uploadUrl;

            var fileSize = file.Length;

            var uploadedSize = 0;
            const int fragmentSize = 60 * 1024 * 1024; //60MiB
            var buffer = new byte[fragmentSize];
            while (uploadedSize < fileSize)
            {
                var request = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
                var readSize = 0;
                if (fileSize - uploadedSize >= fragmentSize)
                {
                    while (readSize < fragmentSize) readSize += file.Read(buffer, readSize, fragmentSize);
                    request.Content = new ByteArrayContent(buffer);
                    request.Content.Headers.ContentRange = new ContentRangeHeaderValue(uploadedSize, uploadedSize + fragmentSize - 1, fileSize);
                }
                else
                {
                    buffer = new byte[fileSize - uploadedSize];
                    while (readSize < fileSize - uploadedSize) readSize += file.Read(buffer, readSize, (int)fileSize - uploadedSize);
                    request.Content = new ByteArrayContent(buffer);
                    request.Content.Headers.ContentRange = new ContentRangeHeaderValue(uploadedSize, fileSize - 1, fileSize);
                }

                response = await hc.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    uploadedSize += fragmentSize;
                }
                else
                {
                    throw new Exception("Error: " + response.ReasonPhrase);
                }
            }

            result = await response.Content.ReadAsStringAsync();
            return Json.Decode(result).id;
        }


        private static async Task<ShareInfo> CreateShareLink(string token, string fileId)
        {
            var url = ApiEndPoint + $"/drive/items/{fileId}/action.createLink";
            var hc = new HttpClient();
            hc.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

            var param = new StringContent(@"{""type"": ""view""}", Encoding.UTF8, "application/json");

            var response = await hc.PostAsync(url, param);
            var result = await response.Content.ReadAsStringAsync();
            var dynamicResult = Json.Decode(result);
            return new ShareInfo { PermissionId = dynamicResult.id, Url = dynamicResult.link.webUrl };
        }

        private static async Task DeleteShareLink(string token, string fileId, string permissionId)
        {
            var url = ApiEndPoint + $"/drive/items/{fileId}/permissions/{permissionId}";
            var hc = new HttpClient();
            hc.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

            var response = await hc.DeleteAsync(url);
            var result = await response.Content.ReadAsStringAsync();
            var dynamicResult = Json.Decode(result);
        }

        private static async Task Delete(string token, string fileId)
        {
            var url = ApiEndPoint + $"/drive/items/{fileId}";
            var hc = new HttpClient();
            hc.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

            var response = await hc.DeleteAsync(url);
            var result = await response.Content.ReadAsStringAsync();
            var dynamicResult = Json.Decode(result);
        }

        public static async Task<string> Upload(int accountNum, string filename, Stream file)
        {
            var refreshToken = new OneDriveDbContext().Accounts.First(x => x.Id == accountNum).RefleshToken;
            var token = await GetAccessToken(refreshToken);
            string fileId;

            if (file.Length < 100 * 1024 * 1024)
            {
                fileId = await Upload(token, filename, file);
            }
            else
            {
                fileId = await UploadLargeFile(token, filename, file);
            }

            return fileId;
        }

        public static async Task<ShareInfo> CreateShareLink(int accountNum, string fileId)
        {
            var refreshToken = new OneDriveDbContext().Accounts.First(x => x.Id == accountNum).RefleshToken;
            var token = await GetAccessToken(refreshToken);
            var shareinfo = await CreateShareLink(token, fileId);

            return shareinfo;
        }

        public static async Task DeleteShareLink(int accountNum, string fileId, string permissionId)
        {
            var refreshToken = new OneDriveDbContext().Accounts.First(x => x.Id == accountNum).RefleshToken;
            var token = await GetAccessToken(refreshToken);
            await DeleteShareLink(token, fileId, permissionId);
        }

        public static async Task Delete(int accountNum, string fileId)
        {
            var refreshToken = new OneDriveDbContext().Accounts.First(x => x.Id == accountNum).RefleshToken;
            var token = await GetAccessToken(refreshToken);
            await Delete(token, fileId);

        }


    }
}