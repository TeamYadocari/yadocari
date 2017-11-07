#region Copyright
/*
 * Yadocari\App_Start\Startup.Auth.cs
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
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;
using Microsoft.Owin.Security.Cookies;
using Owin;
using Yadocari.Models;

namespace Yadocari
{
	public partial class Startup
	{
		// 認証設定の詳細については、http://go.microsoft.com/fwlink/?LinkId=301864 を参照してください
		public void ConfigureAuth(IAppBuilder app)
		{
			// 1 要求につき 1 インスタンスのみを使用するように DB コンテキスト、ユーザー マネージャー、サインイン マネージャーを構成します。
			app.CreatePerOwinContext(ApplicationDbContext.Create);
			app.CreatePerOwinContext<ApplicationUserManager>(ApplicationUserManager.Create);
			app.CreatePerOwinContext<ApplicationSignInManager>(ApplicationSignInManager.Create);

			// アプリケーションが Cookie を使用して、サインインしたユーザーの情報を格納できるようにします
			// また、サードパーティのログイン プロバイダーを使用してログインするユーザーに関する情報を、Cookie を使用して一時的に保存できるようにします
			// サインイン Cookie の設定
			app.UseCookieAuthentication(new CookieAuthenticationOptions
			{
				AuthenticationType = DefaultAuthenticationTypes.ApplicationCookie,
				LoginPath = new PathString("/Account/Login"),
				Provider = new CookieAuthenticationProvider
				{
					// ユーザーがログインするときにセキュリティ スタンプを検証するように設定します。
					// これはセキュリティ機能の 1 つであり、パスワードを変更するときやアカウントに外部ログインを追加するときに使用されます。
					OnValidateIdentity = SecurityStampValidator.OnValidateIdentity<ApplicationUserManager, ApplicationUser>(
									validateInterval: TimeSpan.FromMinutes(30),
									regenerateIdentity: (manager, user) => user.GenerateUserIdentityAsync(manager))
				}
			});
			app.UseExternalSignInCookie(DefaultAuthenticationTypes.ExternalCookie);

			// 2 要素認証プロセスの中で 2 つ目の要素を確認するときにユーザー情報を一時的に保存するように設定します。
			app.UseTwoFactorSignInCookie(DefaultAuthenticationTypes.TwoFactorCookie, TimeSpan.FromMinutes(5));

			// 2 つ目のログイン確認要素 (電話や電子メールなど) を記憶するように設定します。
			// このオプションをオンにすると、ログイン プロセスの中の確認の第 2 ステップは、ログインに使用されたデバイスに保存されます。
			// これは、ログイン時の「このアカウントを記憶する」オプションに似ています。
			app.UseTwoFactorRememberBrowserCookie(DefaultAuthenticationTypes.TwoFactorRememberBrowserCookie);

			// 次の行のコメントを解除して、サード パーティのログイン プロバイダーを使用したログインを有効にします
			//var options = new MicrosoftAccountAuthenticationOptions
			//{
			//	ClientId = "",
			//	ClientSecret = "",
			//	CallbackPath = new PathString("/signin-microsoft")
			//};
			//options.Scope.Add("wl.signin");
			//options.Scope.Add("wl.offline_access");
			//options.Scope.Add("onedrive.readwrite");
			//options.Scope.Add("wl.skydrive_update");
			//app.UseMicrosoftAccountAuthentication(options);

			//app.UseTwitterAuthentication(
			//   consumerKey: "",
			//   consumerSecret: "");

			//app.UseFacebookAuthentication(
			//   appId: "",
			//   appSecret: "");

			//app.UseGoogleAuthentication(new GoogleOAuth2AuthenticationOptions()
			//{
			//    ClientId = "",
			//    ClientSecret = ""
			//});
		}
	}
}