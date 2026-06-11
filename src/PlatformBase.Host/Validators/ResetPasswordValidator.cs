using FluentValidation;
using PlatformBase.Application.Dtos;

namespace PlatformBase.Host.Validators;

/// <summary>
/// 重置密码请求校验器
/// </summary>
public class ResetPasswordValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("密码不能为空")
            .MinimumLength(8).WithMessage("密码至少8位")
            .MaximumLength(128).WithMessage("密码不超过128位")
            .Matches("[A-Z]").WithMessage("密码必须包含大写字母")
            .Matches("[a-z]").WithMessage("密码必须包含小写字母")
            .Matches("[0-9]").WithMessage("密码必须包含数字")
            .Matches("[^a-zA-Z0-9]").WithMessage("密码必须包含特殊字符");
    }
}
