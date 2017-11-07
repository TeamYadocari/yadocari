#region Copyright
/*
 * Yadocari\Migrations2\201601120044427_DownloadCount.cs
 *
 * Copyright (c) 2017 TeamYadocari
 *
 * You can redistribute it and/or modify it under either the terms of
 * the AGPLv3 or YADOCARI binary code license. See the file COPYING
 * included in the YADOCARI package for more in detail.
 *
 */
#endregion
using System.Data.Entity.Migrations;

namespace Yadocari.Migrations2
{
    public partial class DownloadCount : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.OneDriveInfo", "DownloadCount", c => c.Int(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.OneDriveInfo", "DownloadCount");
        }
    }
}
