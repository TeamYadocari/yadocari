#region Copyright
/*
 * Yadocari\Startup.cs
 *
 * Copyright (c) 2017 TeamYadocari
 *
 * You can redistribute it and/or modify it under either the terms of
 * the AGPLv3 or YADOCARI binary code license. See the file COPYING
 * included in the YADOCARI package for more in detail.
 *
 */
#endregion
using Microsoft.Owin;
using Owin;
using Yadocari;

[assembly: OwinStartup(typeof(Startup))]
namespace Yadocari
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
