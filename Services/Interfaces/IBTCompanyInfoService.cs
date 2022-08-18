using BugTracker.Models;

namespace BugTracker.Services.Interfaces
{
    public interface IBTCompanyInfoService
    {
        public Task<Company>GetCompanyInfoByIdAsync(int? companyId); // public keyword not needed in interface. explicit
        public Task<List<BTUser>> GetAllMembersAsync(int companyId);
        public Task<List<Project>> GetAllProjectsAsync(int companyId);
        public Task<List<Ticket>> GetAllTicketAsync(int companyId);
    }
}
