#region Copyright
/*
 * Yadocari\Controllers\ManageController.cs
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
using System.Configuration;
using System.Data.Entity;
using System.Data.Entity.Migrations;
using System.Linq;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;
using System.Web.Hosting;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using S22.Imap;
using Yadocari.Models;
using Yadocari.Service;

namespace Yadocari.Controllers
{
    [Authorize(Roles = nameof(Role.管理))]
    public class ManageController : Controller
    {
        private ApplicationSignInManager _signInManager;
        private ApplicationUserManager _userManager;

        public ManageController()
        {
        }

        public ManageController(ApplicationUserManager userManager, ApplicationSignInManager signInManager)
        {
            UserManager = userManager;
            SignInManager = signInManager;
        }

        public ApplicationSignInManager SignInManager
        {
            get
            {
                return _signInManager ?? HttpContext.GetOwinContext().Get<ApplicationSignInManager>();
            }
            private set
            {
                _signInManager = value;
            }
        }

        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        //
        // GET: /Manage/Index
        public async Task<ActionResult> Index(ManageMessageId? message)
        {
            ViewBag.StatusMessage =
                      message == ManageMessageId.Success ? "成功しました"
                    : message == ManageMessageId.Error ? "エラーが発生しました"
                    : "";

            var userId = User.Identity.GetUserId();
            var model = new IndexViewModel
            {
                HasPassword = HasPassword(),
                IsAdmin = User.IsInRole(nameof(Role.管理)),
                Users = await UserManager.Users.ToListAsync(),
                MSAccounts = await new OneDriveDbContext().Accounts.ToListAsync()
            };
            return View(model);
        }

        //
        // GET: /Manage/ChangePassword
        public ActionResult ChangePassword()
        {
            return View();
        }

        //
        // POST: /Manage/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var result = await UserManager.ChangePasswordAsync(User.Identity.GetUserId(), model.OldPassword, model.NewPassword);
            if (result.Succeeded)
            {
                var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
                if (user != null)
                {
                    await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                }
                return RedirectToAction("Index", new { Message = ManageMessageId.Success });
            }
            AddErrors(result);
            return View(model);
        }

        public ActionResult ManageUsers(ManageMessageId? message)
        {
            ViewBag.StatusMessage =
                      message == ManageMessageId.Success ? "成功しました"
                    : message == ManageMessageId.Error ? "エラーが発生しました"
                    : "";

            ViewBag.Roles = UserManager.Roles;
            return View(UserManager.Users.ToList());
        }

        public ActionResult ManageFiles(ManageMessageId? message)
        {
            ViewBag.StatusMessage =
                      message == ManageMessageId.Success ? "成功しました"
                    : message == ManageMessageId.Error ? "エラーが発生しました"
                    : "";

            var db = new OneDriveDbContext();

            return View(db.Files.ToArray());
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _userManager != null)
            {
                _userManager.Dispose();
                _userManager = null;
            }

            base.Dispose(disposing);
        }

        #region ヘルパー
        // 外部ログインの追加時に XSRF の防止に使用します
        private const string XsrfKey = "XsrfId";

        private IAuthenticationManager AuthenticationManager => HttpContext.GetOwinContext().Authentication;

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
        }

        private bool HasPassword()
        {
            var user = UserManager.FindById(User.Identity.GetUserId());
            if (user != null)
            {
                return user.PasswordHash != null;
            }
            return false;
        }

        private bool HasPhoneNumber()
        {
            var user = UserManager.FindById(User.Identity.GetUserId());
            if (user != null)
            {
                return user.PhoneNumber != null;
            }
            return false;
        }

        public enum ManageMessageId
        {
            Success,
            Error
        }

        #endregion

        public ActionResult MailConfig()
        {
            return View(new MailConfigViewModel()
            {
                MailServer = GetConfiguration<string>("MailServer"),
                AccountName = GetConfiguration<string>("AccountName"),
                Password = GetConfiguration<string>("Password"),
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MailConfigResult(MailConfigViewModel model)
        {
            SetConfiguration("MailServer", model.MailServer);
            SetConfiguration("AccountName", model.AccountName);
            if (model.Password != null)
            {
                SetConfiguration("Password", model.Password);
            }
            return RedirectToAction("Index", new { message = ManageMessageId.Success });
        }

        public ActionResult AddNewAccounts()
        {
            IEnumerable<MailMessage> messages;
            using (var client = new ImapClient(ConfigurationManager.AppSettings["MailServer"], 993,
                    ConfigurationManager.AppSettings["AccountName"], ConfigurationManager.AppSettings["Password"],
                    AuthMethod.Login, true))
            {
                var uids = client.Search(SearchCondition.Unseen());
                messages = client.GetMessages(uids);
            }

            var addedUsers = new List<ApplicationUser>();
            var titleChangedUsers = new List<ApplicationUser>();

            foreach (var message in messages.Where(x => x.Subject.Contains("発表申込完了のお知らせ")))
            {
                var r = new Regex(@"１．整理番号：(?<id>\d+)");
                if (!r.IsMatch(message.Body)) continue;
                var id = r.Match(message.Body).Groups["id"].Value;

                r = new Regex(@"２．パスワード：(?<password>[a-zA-Z0-9]+)");
                if (!r.IsMatch(message.Body)) continue;
                var password = r.Match(message.Body).Groups["password"].Value;

                r = new Regex(@"３．講演題名：(?<title>.+?)------------------------------------------------------------", RegexOptions.Singleline);
                if (!r.IsMatch(message.Body)) continue;
                var title = r.Match(message.Body).Groups["title"].Value.Replace("\r", "").Replace("\n", "");

                var user = new ApplicationUser { UserName = id, Title = title };
                var result = UserManager.Create(user, password);
                if (result.Succeeded)
                {
                    UserManager.AddToRole(user.Id, nameof(Role.アップロード));
                    addedUsers.Add(user);
                }
            }

            foreach (var message in messages.Where(x => x.Subject.Contains("担当研究会申込者更新連絡のお知らせ")))
            {
                var r = new Regex(@"1．整理番号：(?<id>\d+)");
                if (!r.IsMatch(message.Body)) continue;
                var id = r.Match(message.Body).Groups["id"].Value;

                r = new Regex(@"2．タイトル：(?<title>.+?)------------------------------------------------------------", RegexOptions.Singleline);
                if (!r.IsMatch(message.Body)) continue;
                var title = r.Match(message.Body).Groups["title"].Value.Replace("\r", "").Replace("\n", "");

                var user = UserManager.FindByName(id);
                if (user == null) continue;

                var db = new OneDriveDbContext();
                //既にアップロード済みのファイルがある場合はタイトルを変更する
                if (db.Files.Any(x => x.DocumentName == user.Title))
                {
                    var file = db.Files.Single(x => x.DocumentName == user.Title);
                    file.DocumentName = title;
                    db.Files.AddOrUpdate(file);
                    db.SaveChanges();
                }

                user.Title = title;
                var result = UserManager.Update(user);
                if (result.Succeeded)
                {
                    titleChangedUsers.Add(user);
                }
            }

            ViewBag.Roles = UserManager.Roles;
            return View((addedUsers, titleChangedUsers));
        }

        public ActionResult EditUser(string id)
        {
            var user = UserManager.FindById(id);
            if (user == null) return View("Error");

            var roles = UserManager.Roles.Select(role => new SelectListItem()
            {
                Text = role.Name,
                Value = role.Name,
                Selected = user.Roles.Any() && user.Roles.First().RoleId == role.Id
            }).ToList();

            return View(new EditUserViewModel()
            {
                CurrentUserName = user.UserName,
                UserName = user.UserName,
                Title = user.Title,
                Roles = roles,
                Role = user.Roles.Any() ? user.Roles.First().RoleId : null
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditUserResult(EditUserViewModel model)
        {
            var user = UserManager.FindByName(model.CurrentUserName);
            if (user == null) return View("Error");

            ViewBag.UserName = user.UserName;
            user.UserName = model.UserName;
            user.Title = model.Title;
            foreach (var role in Enum.GetNames(typeof(Role)))
            {
                try { UserManager.RemoveFromRole(user.Id, role); } catch (Exception) { }
            }
            UserManager.AddToRole(user.Id, model.Role);
            var userUpdateSucceeded = UserManager.Update(user).Succeeded;
            if (userUpdateSucceeded && model.NewPassword != null)
            {
                var token = UserManager.GeneratePasswordResetToken(user.Id);
                return View(UserManager.ResetPassword(user.Id, token, model.NewPassword).Succeeded);
            }
            return RedirectToAction("ManageUsers", new { message = userUpdateSucceeded ? ManageMessageId.Success : ManageMessageId.Error });
        }

        //
        // GET: /Manage/AddMicrosoftAccount
        public ActionResult AddMicrosoftAccount()
        {
            var clientId = OneDriveService.ClientId;
            var redirectUrl = GetConfiguration<string>("ServerUrl") + Url.Action("AddMicrosoftAccountCallback", "Manage");

            return Redirect($"https://login.live.com/oauth20_authorize.srf?response_type=code&client_id={clientId}&scope=wl.signin%20wl.offline_access%20onedrive.readwrite%20wl.skydrive_update&redirect_uri={redirectUrl}");
        }

        //
        // GET: /Manage/AddMicrosoftAccountCallback
        public async Task<ActionResult> AddMicrosoftAccountCallback(string code)
        {
            var refreshToken = await OneDriveService.GetRefreshToken(code);
            var info = await OneDriveService.GetOwnerInfo(refreshToken);

            var db = new OneDriveDbContext();
            if (db.Accounts.Any(x => x.OneDriveId == info.Id))
            {
                ViewBag.Success = false;
                return View();
            }
            db.Accounts.Add(new Account
            {
                Name = info.DisplayName,
                OneDriveId = info.Id,
                RefleshToken = refreshToken
            });
            db.SaveChanges();

            ViewBag.Success = true;
            return View();
        }

        public class MicrosoftAccount
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string FreeSpace { get; set; }
            public bool Using { get; set; }
        }

        private static string GetHumanReadableSize(long bytes)
        {
            string[] sizes = { "Bytes", "KB", "MB", "GB" };
            double len = bytes;
            var order = 0;
            while (len >= 1024 && order + 1 < sizes.Length)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        public async Task<ActionResult> ManageMicrosoftAccounts()
        {
            var db = new OneDriveDbContext();
            var model = new List<MicrosoftAccount>();

            foreach (var a in db.Accounts)
            {
                model.Add(new MicrosoftAccount
                {
                    Id = a.Id,
                    Name = a.Name,
                    FreeSpace = GetHumanReadableSize((await OneDriveService.GetOwnerInfo(a.RefleshToken)).FreeSpace),
                    Using = GetConfiguration<int>("UsingMicrosoftAccountNum") == a.Id
                });
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangeUsingMicrosoftAccount(int id)
        {
            var db = new OneDriveDbContext();
            if (db.Accounts.Any(account => account.Id == id))
            {
                SetConfiguration("UsingMicrosoftAccountNum", id.ToString());
            }
            else
            {
                return View("Error");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteMicrosoftAccount(int id)
        {
            if (GetConfiguration<int>("UsingMicrosoftAccountNum") == id) return View("Error");

            var db = new OneDriveDbContext();
            if (db.Accounts.Any(account => account.Id == id))
            {
                db.Files.RemoveRange(db.Files.Where(file => file.MicrosoftAccountNum == id));
                db.Accounts.Remove(db.Accounts.First(account => account.Id == id));
                db.SaveChanges();
            }
            else
            {
                return View("Error");
            }
            return View();
        }

        private static T Parse<T>(object obj)
        {
            if (obj == null) return default(T);
            return (T)Convert.ChangeType(obj, typeof(T));
        }

        public static T GetConfiguration<T>(string key)
        {
            var conf = WebConfigurationManager.OpenWebConfiguration(HostingEnvironment.ApplicationVirtualPath);
            return Parse<T>(conf.AppSettings.Settings[key]?.Value);
        }

        public static void SetConfiguration(string key, object value)
        {
            var conf = WebConfigurationManager.OpenWebConfiguration(HostingEnvironment.ApplicationVirtualPath);
            if (conf.AppSettings.Settings.AllKeys.Contains(key))
            {
                conf.AppSettings.Settings[key].Value = value.ToString();
            }
            else
            {
                conf.AppSettings.Settings.Add(key, value.ToString());
            }
            conf.Save();
        }

        public ActionResult SystemConfig()
        {
            return View(new SystemConfigViewModel
            {
                ServerUrl = GetConfiguration<string>("ServerUrl"),
                LinkEnableDuration = GetConfiguration<int>("LinkEnableDuration"),
                CacheDuration = GetConfiguration<int>("CacheDuration"),
                EnableAccountAutoChange = GetConfiguration<bool>("EnableAccountAutoChange"),
                ChangeThreshold = GetConfiguration<int>("ChangeThreshold"),
                ClientId = GetConfiguration<string>("ClientId"),
                ClientSecret = GetConfiguration<string>("ClientSecret")
            });
        }

        public ActionResult SystemConfigResult(SystemConfigViewModel model)
        {
            SetConfiguration("ServerUrl", model.ServerUrl);
            SetConfiguration("LinkEnableDuration", model.LinkEnableDuration);
            SetConfiguration("CacheDuration", model.CacheDuration);
            SetConfiguration("EnableAccountAutoChange", model.EnableAccountAutoChange);
            SetConfiguration("ChangeThreshold", model.ChangeThreshold);
            SetConfiguration("ClientId", model.ClientId);
            SetConfiguration("ClientSecret", model.ClientSecret);

            return RedirectToAction("Index", new { message = ManageMessageId.Success });
        }
    }
}