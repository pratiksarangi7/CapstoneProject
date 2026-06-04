using AutoMapper;
using CapstoneProjectAPI.Models;
using CapstoneProjectAPI.Models.DTOs;

namespace CapstoneProjectAPI.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // ── Auth ───────────────────────────────────────────────────────────────────

            CreateMap<User, RegisterResponseDto>()
                .ForMember(dest => dest.Role,
                           opt => opt.MapFrom(src => src.IsAdmin ? "Admin" : "User"));

            CreateMap<User, LoginResponseDto>()
                .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.IsAdmin ? "Admin" : "User"))
                .ForMember(dest => dest.Token, opt => opt.MapFrom((src, dest, destMember, context) =>
                    context.Items.ContainsKey("JwtToken") ? context.Items["JwtToken"].ToString() : string.Empty));

            // ── Users ──────────────────────────────────────────────────────────────────

            CreateMap<User, UserDetailsResponseDto>()
                .ForMember(dest => dest.DepartmentName,
                           opt => opt.MapFrom(src => src.Department.Name))
                .ForMember(dest => dest.ManagerName,
                           opt => opt.MapFrom(src => src.Manager != null ? src.Manager.Name : null));

            CreateMap<User, DepartmentUserDto>()
                .ForMember(dest => dest.ManagerName,
                           opt => opt.MapFrom(src => src.Manager != null ? src.Manager.Name : null));

            // ── ApprovalAction → ApprovalActionResponseDto ─────────────────────────────
            // ApproverUserName comes from the ApproverUser navigation property.
            // Action enum is converted to its string name.

            CreateMap<ApprovalAction, ApprovalActionResponseDto>()
                .ForMember(dest => dest.ApproverUserName,
                           opt => opt.MapFrom(src => src.ApproverUser != null ? src.ApproverUser.Name : string.Empty))
                .ForMember(dest => dest.Action,
                           opt => opt.MapFrom(src => src.Action.ToString()));


            CreateMap<DocumentVersion, DocumentVersionResponseDto>()
                .ForMember(dest => dest.UploadedByUserName,
                           opt => opt.MapFrom(src => src.UploadedByUser != null ? src.UploadedByUser.Name : string.Empty))
                .ForMember(dest => dest.ApprovalAction,
                           opt => opt.MapFrom((src, dest, destMember, context) =>
                           {
                               if (context.Items.TryGetValue("LatestApprovalAction", out var raw)
                                   && raw is ApprovalAction action
                                   && action.DocumentVersionId == src.Id)
                               {
                                   return context.Mapper.Map<ApprovalActionResponseDto>(action);
                               }
                               // Derive from the collection when available (no context item needed)
                               var latest = src.ApprovalActions
                                   ?.OrderByDescending(a => a.CreatedAt)
                                   .FirstOrDefault();
                               return latest != null
                                   ? context.Mapper.Map<ApprovalActionResponseDto>(latest)
                                   : null;
                           }));

            CreateMap<Document, UserDocumentResponseDto>()
                .ForMember(dest => dest.DocumentStatus,
                           opt => opt.MapFrom(src => src.DocumentStatus.ToString()))
                .ForMember(dest => dest.TargetDepartmentName,
                           opt => opt.MapFrom(src => src.TargetDepartment.Name))
                .ForMember(dest => dest.CurrentApproverName,
                           opt => opt.MapFrom(src => src.CurrentApprover != null ? src.CurrentApprover.Name : null))
                .ForMember(dest => dest.CurrentApproverEmail,
                           opt => opt.MapFrom(src => src.CurrentApprover != null ? src.CurrentApprover.Email : null))
                .ForMember(dest => dest.CurrentApproverDepartmentName,
                           opt => opt.MapFrom(src =>
                               src.CurrentApprover != null && src.CurrentApprover.Department != null
                                   ? src.CurrentApprover.Department.Name
                                   : null))
                .ForMember(dest => dest.CreatedByUserName,
                           opt => opt.MapFrom(src => src.CreatedByUser.Name))
                .ForMember(dest => dest.CreatedByUserEmail,
                           opt => opt.MapFrom(src => src.CreatedByUser.Email))
                .ForMember(dest => dest.Versions,
                           opt => opt.MapFrom(src =>
                               src.Versions.OrderByDescending(v => v.VersionNumber).ToList()));


            CreateMap<Document, UploadDocumentResponseDto>()
                .ForMember(dest => dest.DocumentId,
                           opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.DocumentStatus,
                           opt => opt.MapFrom(src => src.DocumentStatus.ToString()))
                // Version-specific fields injected via context
                .ForMember(dest => dest.VersionNumber,
                           opt => opt.MapFrom((src, dest, destMember, context) =>
                               context.Items.TryGetValue("Version", out var v) && v is DocumentVersion dv
                                   ? dv.VersionNumber : 0))
                .ForMember(dest => dest.OriginalFileName,
                           opt => opt.MapFrom((src, dest, destMember, context) =>
                               context.Items.TryGetValue("Version", out var v) && v is DocumentVersion dv
                                   ? dv.OriginalFileName : string.Empty))
                .ForMember(dest => dest.FileSize,
                           opt => opt.MapFrom((src, dest, destMember, context) =>
                               context.Items.TryGetValue("Version", out var v) && v is DocumentVersion dv
                                   ? dv.FileSize : 0L))
                .ForMember(dest => dest.MimeType,
                           opt => opt.MapFrom((src, dest, destMember, context) =>
                               context.Items.TryGetValue("Version", out var v) && v is DocumentVersion dv
                                   ? dv.MimeType : string.Empty))
                // Runtime approver name fetched by the service after save
                .ForMember(dest => dest.CurrentApproverName,
                           opt => opt.MapFrom((src, dest, destMember, context) =>
                               context.Items.TryGetValue("ApproverName", out var n)
                                   ? n as string : null));

            // ── Document → ApproveDocumentResponseDto ──────────────────────────────────
            // Used by ApproveDocumentAsync (all three action branches) and
            // TransferDocumentAsync. The three fields that live directly on Document
            // (Id, Title, DocumentStatus) are mapped normally. Every other field is
            // runtime-computed and injected by the service via context items:
            //
            //   "ActionTaken"              string   – branch name or "TransferWithoutApproval"
            //   "NewApproverUserId"        int?     – id of new approver user (nullable)
            //   "NewApproverName"          string?  – name of new approver user
            //   "NewApproverDepartmentName"string?  – department of new approver
            //   "ForwardedToDepartmentId"  int?     – target dept id (cross-dept only)
            //   "ForwardedToDepartmentName"string?  – target dept name (cross-dept only)
            //   "ApproverComments"         string?  – free-text from the request
            //   "ActedAt"                  DateTimeOffset – timestamp of the action

            CreateMap<Document, ApproveDocumentResponseDto>()
                .ForMember(dest => dest.DocumentId,
                           opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.DocumentStatus,
                           opt => opt.MapFrom(src => src.DocumentStatus.ToString()))
                .ForMember(dest => dest.ActionTaken,
                           opt => opt.MapFrom((src, dest, destMember, context) =>
                               context.Items.TryGetValue("ActionTaken", out var v)
                                   ? v as string ?? string.Empty : string.Empty))
                .ForMember(dest => dest.NewApproverUserId,
                           opt => opt.MapFrom((src, dest, destMember, context) =>
                               context.Items.TryGetValue("NewApproverUserId", out var v)
                                   ? v as int? : null))
                .ForMember(dest => dest.NewApproverName,
                           opt => opt.MapFrom((src, dest, destMember, context) =>
                               context.Items.TryGetValue("NewApproverName", out var v)
                                   ? v as string : null))
                .ForMember(dest => dest.NewApproverDepartmentName,
                           opt => opt.MapFrom((src, dest, destMember, context) =>
                               context.Items.TryGetValue("NewApproverDepartmentName", out var v)
                                   ? v as string : null))
                .ForMember(dest => dest.ForwardedToDepartmentId,
                           opt => opt.MapFrom((src, dest, destMember, context) =>
                               context.Items.TryGetValue("ForwardedToDepartmentId", out var v)
                                   ? v as int? : null))
                .ForMember(dest => dest.ForwardedToDepartmentName,
                           opt => opt.MapFrom((src, dest, destMember, context) =>
                               context.Items.TryGetValue("ForwardedToDepartmentName", out var v)
                                   ? v as string : null))
                .ForMember(dest => dest.ApproverComments,
                           opt => opt.MapFrom((src, dest, destMember, context) =>
                               context.Items.TryGetValue("ApproverComments", out var v)
                                   ? v as string : null))
                .ForMember(dest => dest.ActedAt,
                           opt => opt.MapFrom((src, dest, destMember, context) =>
                               context.Items.TryGetValue("ActedAt", out var v) && v is DateTimeOffset dt
                                   ? dt : DateTimeOffset.UtcNow));

            // ── DocumentVersion → DocumentFileDto ──────────────────────────────────────
            // MimeType, OriginalFileName and VersionNumber are mapped directly from the
            // entity. FilePath cannot come from the entity (it is computed from
            // IWebHostEnvironment at runtime), so it is injected via context item "FilePath".

            CreateMap<DocumentVersion, DocumentFileDto>()
                .ForMember(dest => dest.FilePath,
                           opt => opt.MapFrom((src, dest, destMember, context) =>
                               context.Items.TryGetValue("FilePath", out var v)
                                   ? v as string ?? string.Empty : string.Empty));
        }
    }
}