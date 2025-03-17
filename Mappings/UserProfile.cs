using AutoMapper;
using modulum.Infrastructure.Models.Identity;
using modulum.Application.Responses.Identity;

namespace modulum.Infrastructure.Mappings
{
    public class UserProfile : Profile
    {
        public UserProfile()
        {
            CreateMap<UserResponse, ModulumUser>().ReverseMap();
        }
    }
}