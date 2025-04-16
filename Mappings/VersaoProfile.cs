using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using modulum.Application.Responses.Versao;
using modulum.Domain.Entities;

namespace modulum.Infrastructure.Mappings
{
    public class VersaoProfile : Profile
    {
        public VersaoProfile() 
        {
            CreateMap<VersaoResponse, NugetPacote>().ReverseMap();
        }
    }
}
