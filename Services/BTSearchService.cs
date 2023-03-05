using BugTracker.Data;
using BugTracker.Models;
using BugTracker.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using BugTracker.Services;

namespace BugTracker.Services
{
	public class BTSearchService : IBTSearchService
	{
		private readonly ApplicationDbContext _context;
		private readonly IBTProjectService _projectService;
		private readonly IBTTicketService _ticketService;

        public BTSearchService(ApplicationDbContext context,
                               IBTProjectService projectService,
                               IBTTicketService ticketService)
        {
            _context = context;
            _projectService = projectService;
            _ticketService = ticketService;
        }

        public async Task<SearchResult> Search(string searchTerm, int companyId)
		{
			SearchResult results = new();

			var projects = await _projectService.GetAllProjectsByCompanyAsync(companyId);
			var tickets = await _ticketService.GetAllTicketsByCompanyAsync(companyId);

			if (searchTerm != null)
			{
				searchTerm = searchTerm.ToLower();

				projects = projects.Where(
					p => p.Name.ToLower().Contains(searchTerm) ||
					p.Members.Any(m => m.FirstName.ToLower().Contains(searchTerm) ||
									   m.LastName.ToLower().Contains(searchTerm) ||
									   m.Email.ToLower().Contains(searchTerm)) ||
					p.Description.ToLower().Contains(searchTerm))
					.ToList();

				tickets = tickets.Where(
					t => t.Title.ToLower().Contains(searchTerm) ||
                    t.Description.ToLower().Contains(searchTerm)).ToList();
			}

			results.Projects = projects.OrderByDescending(p => p.EndDate).ToList();
			results.Tickets = tickets.OrderByDescending(t => t.Created).ToList();

			return results;
		}
    }
}
