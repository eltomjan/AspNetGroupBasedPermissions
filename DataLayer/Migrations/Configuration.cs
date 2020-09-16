namespace DataLayer.Migrations
{
    using System.Collections.Generic;
    using System.Data.Entity.Migrations;
    using System.Linq;
    using DataClasses.Concrete;

    internal sealed class Configuration : DbMigrationsConfiguration<DataClasses.AspNetGroupBasedPermissionsDb>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = false;
        }

        protected override void Seed(DataClasses.AspNetGroupBasedPermissionsDb context)
        {
            if (!context.Log.Any())
            {
                context.Log.Add(new DataClasses.Log("EFCF", "Info", "Initial Seed"));
            }
        }

    }
}
