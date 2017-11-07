#region Copyright
/*
 * Yadocari\Migrations2\201601041555112_Init.cs
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
  public partial class Init : DbMigration
  {
    public override void Up()
    {
      CreateTable(
          "dbo.OneDriveInfo",
          c => new
          {
            Id = c.Int(nullable: false, identity: true),
            DocumentId = c.Int(nullable: false),
            DocumentName = c.String(),
            OneDriveUrl = c.String(),
          })
          .PrimaryKey(t => t.Id);

    }

    public override void Down()
    {
      DropTable("dbo.OneDriveInfo");
    }
  }
}
