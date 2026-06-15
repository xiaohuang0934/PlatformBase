using FluentValidation;

namespace PlatformBase.Host.Validators;

/// <summary>
/// 创建用户校验器 — 密码复杂度 + 用户名唯一性（异步查 DB）
/// </summary>
public class CreateUserValidator : AbstractValidator<CreateUserDto>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("用户名不能为空")
            .MinimumLength(3).WithMessage("用户名至少3个字符")
            .MaximumLength(50).WithMessage("用户名不超过50个字符");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("密码不能为空")
            .MinimumLength(8).WithMessage("密码至少8位")
            .MaximumLength(128).WithMessage("密码不超过128位")
            .Matches("[A-Z]").WithMessage("密码必须包含大写字母")
            .Matches("[a-z]").WithMessage("密码必须包含小写字母")
            .Matches("[0-9]").WithMessage("密码必须包含数字")
            .Matches("[^a-zA-Z0-9]").WithMessage("密码必须包含特殊字符");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("邮箱格式不正确")
            .When(x => !string.IsNullOrEmpty(x.Email));
    }
}
