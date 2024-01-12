﻿using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NoMercy.Database.Models
{
    [PrimaryKey(nameof(Id))]
    public class MediaStream
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public required string Id { get; set; }
        
    }
}