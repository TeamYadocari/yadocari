#region Copyright
/*
 * Yadocari\Migrations2\201601161825486_EditAccount.cs
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
    public partial class EditAccount : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Accounts", "OneDriveId", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Accounts", "OneDriveId");
        }
    }
}
