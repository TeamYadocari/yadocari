#region Copyright
/*
 * Yadocari\Migrations\201601181804262_AccountNum.cs
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

namespace Yadocari.Migrations
{
    public partial class AccountNum : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Files", "MicrosoftAccountNum", c => c.Int(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Files", "MicrosoftAccountNum");
        }
    }
}
