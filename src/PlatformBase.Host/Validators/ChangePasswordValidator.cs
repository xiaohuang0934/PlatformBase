using FluentValidation;

namespace PlatformBase.Host.Validators;

/// <summary>
/// 修改密码校验器 — 新密码 ≠ 当前密码（跨字段校验）
/// </summary>
public class ChangePasswordValidator : AbstractValidator<ChangePasswordDto>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("当前密码不能为空");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("新密码不能为空")
            .MinimumLength(8).WithMessage("新密码至少8位")
            .MaximumLength(128).WithMessage("新密码不超过128位")
            .Matches("[A-Z]").WithMessage("新密码必须包含大写字母")
            .Matches("[a-z]").WithMessage("新密码必须包含小写字母")
            .Matches("[0-9]").WithMessage("新密码必须包含数字")
            .Matches("[^a-zA-Z0-9]").WithMessage("新密码必须包含特殊字符")
            .NotEqual(x => x.CurrentPassword).WithMessage("新密码不能与当前密码相同");
    }
}
