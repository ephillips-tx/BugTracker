#nullable disable 
using BugTracker.Data;
using BugTracker.Models;
using BugTracker.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BugTracker.Services
{
    public class BTCompanyInfoService : IBTCompanyInfoService
    {
        private readonly ApplicationDbContext _context;

        public BTCompanyInfoService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<BTUser>> GetAllMembersAsync(int companyId)
        {
            List<BTUser> result = new(); // constructor creating a list of type BTUser
            result = await _context.Users.Where(u => u.CompanyId == companyId).ToListAsync(); // add results to list
            return result; // return result
        }

        public async Task<List<Project>> GetAllProjectsAsync(int companyId)
        {
            List<Project> result = new(); // constructor creating a list of type Project -- INTERMEDIATE variable -- can condense code
            result = await _context.Projects.Where(p => p.CompanyId == companyId)  // Include = Eager Loading...
                                            .Include(p => p.Members)               // Which members on this project
                                            .Include(p => p.Tickets)               // Which tickets are linked to this project
                                                .ThenInclude(t => t.Comments)         // Get comments associated with ticket
                                            .Include(p => p.Tickets)
                                                .ThenInclude(t => t.Attachments)      // Get ticket attachments
                                            .Include(p => p.Tickets)
                                                .ThenInclude(t => t.Notifications)    // Get ticket notifications
                                            .Include(p => p.Tickets)
                                                .ThenInclude(t => t.History)          // Get ticket history
                                            .Include(p => p.Tickets)
                                                .ThenInclude(t => t.DeveloperUser)    // Get ticket developer
                                            .Include(p => p.Tickets)
                                                .ThenInclude(t => t.OwnerUser)        // Get ticket owner
                                            .Include(p => p.Tickets)           
                                                .ThenInclude(t => t.TicketStatus)     // Get status associated with ticket
                                            .Include(p => p.Tickets)               
                                                .ThenInclude(t => t.TicketPriority)   // Get priority of ticket
                                            .Include(p => p.Tickets)
                                                .ThenInclude(t => t.TicketType)       // Get type of ticket
                                            .Include(p => p.ProjectPriority)       // What is the priority of the project
                                            .ToListAsync();                        // add results to list
            return result;                                                         // return result
        }

        public async Task<List<Ticket>> GetAllTicketAsync(int companyId)
        {
            List<Ticket> result = new();
            List<Project> projects = new();

            projects = await GetAllProjectsAsync(companyId);

            result = projects.SelectMany(p => p.Tickets).ToList();

            return result;
        }

        public async Task<Company> GetCompanyInfoByIdAsync(int? companyId)
        {
            Company result = new();

            if (companyId != null)
            {
                result = await _context.Companies
                                        .Include(c => c.Members)
                                        .Include(c => c.Projects)
                                        .Include(c => c.Invites)
                                        .FirstOrDefaultAsync(c => c.Id == companyId);
            }
            return result;
        }
    }
}
