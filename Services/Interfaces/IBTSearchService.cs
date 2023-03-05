using BugTracker.Models;

namespace BugTracker.Services.Interfaces
{
	public interface IBTSearchService
	{
		public Task<SearchResult> Search(string searchTerm, int companyId);
	}
}
