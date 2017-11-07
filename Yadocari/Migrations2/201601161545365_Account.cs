#region Copyright
/*
 * Yadocari\Migrations2\201601161545365_Account.cs
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
    public partial class Account : DbMigration
    {
        public override void Up()
        {
            RenameTable(name: "dbo.OneDriveInfo", newName: "Files");
            CreateTable(
                "dbo.Accounts",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(),
                        RefleshToken = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
        }
        
        public override void Down()
        {
            DropTable("dbo.Accounts");
            RenameTable(name: "dbo.Files", newName: "OneDriveInfo");
        }
    }
}
