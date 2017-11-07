#region Copyright
/*
 * Yadocari\Controllers\HomeController.cs
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Yadocari.Models;
using Yadocari.Service;
using File = Yadocari.Models.File;

namespace Yadocari.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private static async Task DeleteShareLinkTask(int accountNum, string fileId, string permissionId)
        {
            await Task.Delay(TimeSpan.FromMinutes(ManageController.GetConfiguration<int>("LinkEnableDuration")));
            await OneDriveService.DeleteShareLink(accountNum, fileId, permissionId);
        }

        private static List<Task> _tasks = new List<Task>(); //GCに消されないように保持しておく

        [AllowAnonymous]
        public ActionResult About()
        {
            ViewBag.Message = "本システムの一般的な利用法など.";

            return View();
        }

        [AllowAnonymous]
        public ActionResult Contact()
        {
            ViewBag.Message = "本システムに関する連絡先．";

            return View();
        }

        public new class Content
        {
            public string Title { get; set; }
            public int Count { get; set; } //-1→コンテンツ
            public string Url { get; set; }
            public int Id { get; set; }
            public int DownloadCount { get; set; }
            public bool Uploaded { get; set; }

            public Content(string title, int count, string url, int id)
            {
                Title = title;
                Count = count;
                Url = url;
                Id = id;

                var db = new OneDriveDbContext();

                //アップロード済みファイルとの関連付け
                if (db.Files.Any(file => file.DocumentId == -1 && file.DocumentName == title))
                {
                    db.Files.First(file => file.DocumentName == title).DocumentId = id;
                    db.SaveChanges();
                }

                if (count == -1 && db.Files.Any(x => x.DocumentId == id))
                {
                    var item = db.Files.First(x => x.DocumentId == id);
                    Uploaded = item.OneDriveFileId != "";
                    DownloadCount = item.DownloadCount;
                }
            }
        }

        private class ContentsCache
        {
            public DateTime CachedAt { get; set; }
            public IEnumerable<Content> Contents { get; set; }
        }

        private static readonly Dictionary<int, ContentsCache> Cache = new Dictionary<int, ContentsCache>();

        [UploadUserRedirect]
        public ActionResult Index()
        {
            return View();
        }

        [UploadUserRedirect]
        public ActionResult Contents(int id = -1)
        {
            if (id == -1) id = 4088; //IOTトップ

            //キャッシュを利用する
            if (Cache.ContainsKey(id) && DateTime.Now - Cache[id].CachedAt <= TimeSpan.FromMinutes(ManageController.GetConfiguration<int>("CacheDuration")))
            {
                Debug.WriteLine("キャッシュ利用");
                return View(Cache[id].Contents);
            }

            var url = $"https://ipsj.ixsq.nii.ac.jp/ej/?action=repository_opensearch&index_id={id}&count=100&order=7";
            var wc = new WebClient { Encoding = Encoding.UTF8 };
            var str = wc.DownloadString(url);
            var regFolder = new Regex(
                @"<td class=""pl10 pt10 vat"" width=""100%""><span><a href=""(?<url>https://ipsj\.ixsq\.nii\.ac\.jp/ej/\?action=repository_opensearch&amp;index_id=(?<indexId>\d+)&amp;count=100&amp;order=7&amp;pn=1)"">(?<title>.+)</a><wbr /><span class=""text_color"">&nbsp;\[(?<count>\d+)件</span><span class=""text_color"">\]&nbsp;</span></span></td>");
            var results = regFolder.Matches(str);
            var contents = (from Match result in results select new Content(result.Groups["title"].Value, int.Parse(result.Groups["count"].Value), result.Groups["url"].Value, int.Parse(result.Groups["indexId"].Value)));

            if (!contents.Any()) //フォルダが一つも無い→コンテンツ
            {
                var regContents = new Regex(
                    @"<div class=""item_title pl55"">\s+<a href=""(?<url>https://ipsj\.ixsq\.nii\.ac\.jp/ej/\?action=repository_uri&item_id=(?<itemId>\d+))"">\s+(?<title>.+)\s+</a>\s+</div>");
                results = regContents.Matches(str);

                contents = from Match result in results select new Content(result.Groups["title"].Value, -1, result.Groups["url"].Value, int.Parse(result.Groups["itemId"].Value));
            }

            //キャッシュに登録
            if (Cache.ContainsKey(id))
            {
                Cache.Remove(id);
            }
            Debug.WriteLine("キャッシュ登録");
            Cache.Add(id, new ContentsCache() { CachedAt = DateTime.Now, Contents = contents });
            return View(contents);
        }

        [UploadUserRedirect]
        public ActionResult UnregisteredContents()
        {
            var db = new OneDriveDbContext();
            var files = from File file in db.Files.Where(file => file.DocumentId == -1) select file;
            var list = new List<Content>();
            foreach (var file in files)
            {
                list.Add(new Content(file.DocumentName, -1, null, -1));
            }
            return View(list);
        }

        private static readonly Dictionary<string, byte[]> TemporartZipCache = new Dictionary<string, byte[]>();

        private static string GetTitleById(int id)
        {
            var wc = new WebClient { Encoding = Encoding.UTF8 };
            var str = wc.DownloadString($"https://ipsj.ixsq.nii.ac.jp/ej/?action=repository_uri&item_id={id}");
            var reg = new Regex(@"<meta name=""citation_title"" content=""(?<title>.+?)""/>");
            return reg.Match(str).Groups["title"].Value;
        }

        public ActionResult AddFile(int id = -1)
        {
            ViewBag.AlreadyExisting = false;
            var db = new OneDriveDbContext();

            if (User.IsInRole(nameof(Role.アップロード)))
            {
                var manager = HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
                string name = ViewBag.FileName = manager.FindByName(User.Identity.Name).Title;

                if (db.Files.Any(info => info.DocumentName == name))
                {
                    ViewBag.AlreadyExisting = true;
                }

                return View();
            }

            if (!User.IsInRole(nameof(Role.管理)) || id == -1) return View("Error");

            var title = GetTitleById(id);

            ViewBag.id = id;
            ViewBag.FileName = title;

            if (db.Files.Any(info => info.DocumentId == id))
            {
                ViewBag.AlreadyExisting = true;
            }

            return View();

        }

        [Authorize(Roles = nameof(Role.管理))]
        public ActionResult ChangeAssosiation(int id = -1, string title = "")
        {
            if (id == -1 && title == "") return View("Error");
            if (title == "")
            {
                title = GetTitleById(id);
            }

            var db = new OneDriveDbContext();
            var model = new ChangeAssosiationViewModel { Title = title, CurrentId = id, NewId = id };
            return db.Files.Any(x => id != -1 ? x.DocumentId == id : x.DocumentName == title) ? View(model) : View("Error");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = nameof(Role.管理))]
        public ActionResult ChangeAssosiationResult(ChangeAssosiationViewModel model)
        {
            var db = new OneDriveDbContext();
            var userManager = HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();

            if (model.CurrentId == -1)
            {
                if (db.Files.Any(x => x.DocumentName == model.Title) && userManager.Users.Any(x => x.Title == model.Title))
                {
                    if (model.NewId == -1 || !db.Files.Any(x => x.DocumentId == model.NewId))
                    {
                        db.Files.First(x => x.DocumentName == model.Title).DocumentId = model.NewId;
                        var user = userManager.Users.First(x => x.Title == model.Title);
                        user.Title = GetTitleById(model.NewId);
                        if (userManager.Update(user).Succeeded)
                        {
                            db.SaveChanges();
                            return View(true);
                        }
                    }
                }
            }
            else if (db.Files.Any(x => x.DocumentId == model.CurrentId))
            {
                if (model.NewId == -1 || !db.Files.Any(x => x.DocumentId == model.NewId))
                {
                    db.Files.First(x => x.DocumentId == model.CurrentId).DocumentId = model.NewId;
                    db.SaveChanges();
                    return View(true);
                }
            }
            return View(false);
        }

        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0);

        private static long GetUnixTime(DateTime targetTime)
        {
            targetTime = targetTime.ToUniversalTime();
            var elapsedTime = targetTime - UnixEpoch;
            return (long)elapsedTime.TotalSeconds;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = nameof(Role.管理) + ", " + nameof(Role.アップロード))]
        public async Task<ActionResult> Upload(int id = -1)
        {
            //許可された拡張子
            var allowedExtentions = new[]
            {
                ".pptx",
                ".ppt",
                ".pptm",
                ".ppsx",
                ".pps",
                ".ppsm",
                ".pdf",
                ".zip"
            };
            ViewBag.success = false;
            var db = new OneDriveDbContext();

            //ファイルが無い
            if (Request.Files.Count <= 0) return View("Error");
            var file = Request.Files[0];
            if (file?.FileName == null || file.ContentLength <= 0) return View("Error");

            //許可された拡張子ではない
            if (allowedExtentions.All(ext => Path.GetExtension(file.FileName).ToLower() != ext))
            {
                return View("UploadFinished");
            }

            //ファイル名は"UnixTime_元のファイル名"
            var fileName = $"{GetUnixTime(DateTime.Now)}_{Path.GetFileName(file.FileName)}";
            var tempFileName = Path.GetTempFileName();
            file.SaveAs(tempFileName);
            var stream = new FileStream(tempFileName, FileMode.Open, FileAccess.Read, FileShare.None, 8, FileOptions.DeleteOnClose);

            try
            {
                var manager = HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
                var accountId = ManageController.GetConfiguration<int>("UsingMicrosoftAccountNum");
                var fileId = await OneDriveService.Upload(accountId, fileName, stream);

                if (ManageController.GetConfiguration<bool>("EnableAccountAutoChange"))
                {
                    var currentAccount = await OneDriveService.GetOwnerInfo(db.Accounts.Single(x => x.Id == accountId).RefleshToken);
                    var threshold = ManageController.GetConfiguration<int>("ChangeThreshold");
                    if ((double)currentAccount.FreeSpace / 1024 / 1024 < threshold)
                    {
                        var preload = db.Accounts.Select(x => OneDriveService.GetOwnerInfo(x.RefleshToken));
                        await Task.WhenAll(preload);
                        var nextAccount = preload.Select(x => x.Result).FirstOrDefault(x => (double)x.FreeSpace / 1024 / 1024 >= threshold);
                        if (nextAccount != null) ManageController.SetConfiguration("UsingMicrosoftAccountNum", nextAccount.Id);
                    }
                }

                if (string.IsNullOrWhiteSpace(fileId))
                {
                    ViewBag.success = false;
                    return View("UploadFinished");
                }
                if (User.IsInRole(nameof(Role.管理)))
                {
                    //既に登録済みのファイルIDである
                    if (db.Files.Any(x => x.DocumentId == id)) return View("Error");
                    var wc = new WebClient { Encoding = Encoding.UTF8 };
                    var str = wc.DownloadString($"https://ipsj.ixsq.nii.ac.jp/ej/?action=repository_uri&item_id={id}");
                    var reg = new Regex(@"<meta name=""citation_title"" content=""(?<title>.+?)""/>");
                    var title = reg.Match(str).Groups["title"].Value;
                    db.Files.Add(new File
                    {
                        DocumentId = id,
                        DocumentName = title,
                        MicrosoftAccountNum = accountId,
                        OneDriveFileId = fileId
                    });
                }
                else if (User.IsInRole(nameof(Role.アップロード)))
                {
                    var name = manager.FindByName(User.Identity.Name).Title;
                    //既に登録済みのタイトルである
                    if (db.Files.Any(x => x.DocumentName == name)) return View("Error");
                    db.Files.Add(new File
                    {
                        DocumentId = -1,
                        DocumentName = name,
                        MicrosoftAccountNum = accountId,
                        OneDriveFileId = fileId
                    });
                }
                else
                {
                    //権限がない
                    return View("Error");
                }
                await db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return View("Error");
            }

            ViewBag.success = true;
            return View("UploadFinished");
        }

        public async Task<ActionResult> ShowDocument(int id = -1, string title = "")
        {
            var db = new OneDriveDbContext();
            File file;
            if (id != -1)
            {
                //権限がない
                if (User.IsInRole(nameof(Role.アップロード))) return View("Error");

                file = db.Files.First(info => info.DocumentId == id);
            }
            else
            {
                if (title == "")
                {
                    var manager = HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
                    var name = manager.FindByName(User.Identity.Name).Title;
                    //該当する資料が存在しない
                    if (!db.Files.Any(info => info.DocumentName == name)) return View("Error");

                    file = db.Files.First(info => info.DocumentName == name);
                }
                else
                {
                    //権限がない
                    if (User.IsInRole(nameof(Role.アップロード))) return View("Error");

                    var manager = HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
                    //該当する資料が存在しない
                    if (!db.Files.Any(info => info.DocumentName == title)) return View("Error");

                    file = db.Files.First(info => info.DocumentName == title);
                }
            }

            file.DownloadCount++;
            db.SaveChanges();
            var shareInfo = await OneDriveService.CreateShareLink(file.MicrosoftAccountNum, file.OneDriveFileId);
            _tasks.Add(DeleteShareLinkTask(file.MicrosoftAccountNum, file.OneDriveFileId, shareInfo.PermissionId));
            _tasks = _tasks.Where(x => !x.IsCompleted || !x.IsCanceled || !x.IsFaulted).ToList(); //完了済みのものを取り除く
            return Redirect(shareInfo.Url);

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = nameof(Role.管理) + ", " + nameof(Role.アップロード))]
        public async Task<ActionResult> DeleteDocument(int documentId = -1, string title = "")
        {
            var db = new OneDriveDbContext();
            File file;
            if (User.IsInRole(nameof(Role.管理)))
            {
                if (title == "")
                {
                    if (documentId != -1 && !db.Files.Any(info => info.DocumentId == documentId)) return View("Error");
                    file = db.Files.First(info => info.DocumentId == documentId);
                }
                else
                {
                    if (!db.Files.Any(info => info.DocumentName == title)) return View("Error");
                    file = db.Files.First(info => info.DocumentName == title);
                }
            }
            else if (User.IsInRole(nameof(Role.アップロード)))
            {
                var manager = HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
                var name = manager.FindByName(User.Identity.Name).Title;
                if (!db.Files.Any(info => info.DocumentName == name)) return View("Error");
                file = db.Files.First(info => info.DocumentName == name);
            }
            else
            {
                //権限がない
                return View("Error");
            }

            await OneDriveService.Delete(file.MicrosoftAccountNum, file.OneDriveFileId);
            db.Files.Remove(file);
            await db.SaveChangesAsync();
            return View();
        }
    }

    /// <summary>
    /// Uploadユーザをリダイレクトさせる
    /// </summary>
    public class UploadUserRedirectAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.HttpContext.User.IsInRole(nameof(Role.アップロード)))
            {
                context.Result = new RedirectResult("/Home/AddFile");
                return;
            }
            base.OnActionExecuting(context);
        }
    }
}