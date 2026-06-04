using AutoMapper;
using CapstoneProjectAPI.Models;
using CapstoneProjectAPI.Models.DTOs;

namespace CapstoneProjectAPI.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<User, RegisterResponseDto>().ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.IsAdmin ? "Admin" : "User"));
            CreateMap<User, LoginResponseDto>()
    .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.Id))
    .ForMember(dest => dest.Role,
               opt => opt.MapFrom(src => src.IsAdmin ? "Admin" : "User"))
    .ForMember(dest => dest.Token, opt => opt.MapFrom((src, dest, destMember, context) => 
                context.Items.ContainsKey("JwtToken") ? context.Items["JwtToken"].ToString() : string.Empty));
            // User → UserDetailsResponseDto
            CreateMap<User, UserDetailsResponseDto>()
                .ForMember(dest => dest.DepartmentName,
                           opt => opt.MapFrom(src => src.Department.Name))
                .ForMember(dest => dest.ManagerName,
                           opt => opt.MapFrom(src => src.Manager != null ? src.Manager.Name : null));
            // User → DepartmentUserDto
            CreateMap<User, DepartmentUserDto>()
                .ForMember(dest => dest.ManagerName,
                           opt => opt.MapFrom(src => src.Manager != null ? src.Manager.Name : null));

        }
    }
}