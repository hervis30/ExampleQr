using Microsoft.EntityFrameworkCore;

namespace WebMVCTest.Data
{
    public class ApplicationDbContext: DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options):base(options){}

        //agretgar modelo

    }
}
