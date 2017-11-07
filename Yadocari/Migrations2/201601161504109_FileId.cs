#region Copyright
/*
 * Yadocari\Migrations2\201601161504109_FileId.cs
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
    public partial class FileId : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.OneDriveInfo", "OneDriveFileId", c => c.String());
            DropColumn("dbo.OneDriveInfo", "OneDriveUrl");
        }
        
        public override void Down()
        {
            AddColumn("dbo.OneDriveInfo", "OneDriveUrl", c => c.String());
            DropColumn("dbo.OneDriveInfo", "OneDriveFileId");
        }
    }
}
