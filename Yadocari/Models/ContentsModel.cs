#region Copyright
/*
 * Yadocari\Models\ContentsModel.cs
 *
 * Copyright (c) 2017 TeamYadocari
 *
 * You can redistribute it and/or modify it under either the terms of
 * the AGPLv3 or YADOCARI binary code license. See the file COPYING
 * included in the YADOCARI package for more in detail.
 *
 */
#endregion
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;

namespace Yadocari.Models
{
	public class File
	{
		public int Id { get; set; }
        [Index(IsUnique = true)]
        public int DocumentId { get; set; }
        [Index(IsUnique = true)]
        [StringLength(256)]
        public string DocumentName { get; set; }
		public int MicrosoftAccountNum { get; set; }
		public string OneDriveFileId { get; set; }
		public int DownloadCount { get; set; }
	}

	public class Account
	{
		public int Id { get; set; }
        [Index(IsUnique = true)]
        [StringLength(256)]
        public string OneDriveId { get; set; }
		public string Name { get; set; }
		public string RefleshToken { get; set; }
	}

	public class OneDriveDbContext : DbContext
	{
		public DbSet<File> Files { get; set; }
		public DbSet<Account> Accounts { get; set; }
	}

	public class ChangeAssosiationViewModel
	{
		[Required]
		[Display(Name = "発表題名")]
		public string Title { get; set; }

		[Display(Name = "現在の電子図書館上のID")]
		public int CurrentId { get; set; }

		[Required]
		[Display(Name = "電子図書館上のID")]
		public int NewId { get; set; }
	}
}
