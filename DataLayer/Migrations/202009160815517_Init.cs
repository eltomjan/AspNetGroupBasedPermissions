namespace DataLayer.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Init : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Log",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Application = c.String(nullable: false, maxLength: 50),
                        Logged = c.DateTime(nullable: false),
                        Level = c.String(nullable: false, maxLength: 50),
                        Message = c.String(nullable: false),
                        Logger = c.String(maxLength: 250),
                        Callsite = c.String(),
                        Exception = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.Id, unique: true);
            
            CreateTable(
                "dbo.Users",
                c => new
                    {
                        UserId = c.Int(nullable: false, identity: true),
                        Mail = c.String(nullable: false, maxLength: 128),
                    })
                .PrimaryKey(t => t.UserId);
            
        }
        
        public override void Down()
        {
            DropIndex("dbo.Log", new[] { "Id" });
            DropTable("dbo.Users");
            DropTable("dbo.Log");
        }
    }
}
