using AutoMapper;
using modulum.Application.Requests.Dynamic;
using modulum.Application.Requests.Dynamic.Create;
using modulum.Application.Responses.Identity;
using modulum.Domain.Entities.DynamicEntity;
using modulum.Domain.Enums;
using modulum.Infrastructure.Models.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace modulum.Infrastructure.Mappings
{
    public class TableProfiler : Profile
    {
        public TableProfiler() 
        {
            CreateMap<CreateDynamicFieldRequest, Field>().ReverseMap();
            CreateMap<CreateDynamicTableRequest, Table>()
            .ForMember(dest => dest.Fields, opt => opt.MapFrom(src => src.Campos))
            .ReverseMap()
            .ForMember(dest => dest.Campos, opt => opt.MapFrom(src => src.Fields));
            CreateMap<Table, MenuRequest>();
        }
    }
}
