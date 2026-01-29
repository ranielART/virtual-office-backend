//using Persistence.Contexts;
using Persistence.Data;
using System.Data;

namespace Persistence.Seeders
{
    public class DataSeeder : IDataSeeder
    {
        private readonly ApplicationDbContext _context;

        public DataSeeder(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task SeedAsync()
        {
            // Add seed data logic here
            await Task.CompletedTask;
        }
    }
}