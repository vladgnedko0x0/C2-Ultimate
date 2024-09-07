using C2_Ultimate.Models;
using Microsoft.EntityFrameworkCore;

namespace C2_Ultimate.DBContext
{
    public class AppDbContext:DbContext
    {
        public DbSet<User> Users { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseMySql("Server=127.127.126.26;Database=C2_DB;User=root;Password=",
           new MySqlServerVersion(new Version(8, 0, 23)),
           mySqlOptions => mySqlOptions.EnableRetryOnFailure());
        }
    }
}
