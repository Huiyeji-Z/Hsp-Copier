namespace HspCopier.Animations.DependencyInjection;

using System.Collections.Generic;
using HspCopier.Core.Animations;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// 动画相关 DI 注册扩展。
/// </summary>
public static class AnimationServiceCollectionExtensions
{
    public static IServiceCollection AddHspCopierAnimations(this IServiceCollection services)
    {
        services.AddSingleton<ITransformAnimation, ScaleFadeAnimation>();
        services.AddSingleton<IAnimationEngine, AnimationEngine>();
        return services;
    }
}
