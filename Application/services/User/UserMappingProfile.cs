using Application.Services.User.Response;
using AutoMapper;
using DomainEntity = Domain.Entity.Identity.User;

namespace Application.Services.User
{
    public class UserMappingProfile : Profile
    {
        public UserMappingProfile()
        {
            // Map User entity to CreateUserResponse
            CreateMap<DomainEntity, CreateUserResponse>()
                .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Message, opt => opt.Ignore());

            // Map User entity to LoginResponse
            CreateMap<DomainEntity, LoginResponse>()
                .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.AccessToken, opt => opt.Ignore())
                .ForMember(dest => dest.RefreshToken, opt => opt.Ignore());
        }
    }
}
