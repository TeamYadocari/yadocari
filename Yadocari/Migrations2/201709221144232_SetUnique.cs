#region Copyright
/*
 * Yadocari\Migrations2\201709221144232_SetUnique.cs
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
    public partial class SetUnique : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.Accounts", "OneDriveId", c => c.String(maxLength: 256));
            AlterColumn("dbo.Files", "DocumentName", c => c.String(maxLength: 256));
            CreateIndex("dbo.Accounts", "OneDriveId", unique: true);
            CreateIndex("dbo.Files", "DocumentId", unique: true);
            CreateIndex("dbo.Files", "DocumentName", unique: true);
        }
        
        public override void Down()
        {
            DropIndex("dbo.Files", new[] { "DocumentName" });
            DropIndex("dbo.Files", new[] { "DocumentId" });
            DropIndex("dbo.Accounts", new[] { "OneDriveId" });
            AlterColumn("dbo.Files", "DocumentName", c => c.String());
            AlterColumn("dbo.Accounts", "OneDriveId", c => c.String());
        }
    }
}
