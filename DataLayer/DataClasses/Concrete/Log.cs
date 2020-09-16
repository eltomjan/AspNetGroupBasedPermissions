using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataLayer.DataClasses
{
    [Table("Log")]
    public class Log
    {
        public Log() { }
        public Log(string application, string level, string message)
        {
            Application = application;
            Logged = DateTime.Now;
            Level = level;
            Message = message;
        }

        [Index(IsUnique = true)]
        [Key]
        public virtual int Id { get; private set; }

        [MaxLength(50)]
        [Required]
        public string Application { get; private set; }
        public DateTime Logged { get; private set; }
        [MaxLength(50)]
        [Required]
        public string Level { get; private set; }
        [Required]
        public string Message { get; private set; }
        [MaxLength(250)]
        public string Logger { get; private set; }
        public string Callsite { get; private set; }
        public string Exception { get; private set; }
    }
}