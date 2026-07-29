using System.Reflection;

using FluentAssertions;
using Xunit;

using Pica.Viewer.Services;
using Pica.Viewer.Views;

namespace Pica.Viewer.Tests.Services;

public sealed class ImageViewerArchitectureTests
{
    [Fact]
    public void ImageViewerWindow_WhenInspected_DoesNotOwnLoadingOrPlatformServices()
    {
        Type[] forbiddenDependencies =
        [
            typeof(ImageLoadCoordinator),
            typeof(AvaloniaViewerFilePickerService),
            typeof(AvaloniaClipboardDataWriter),
            typeof(ViewerImageCommandService)
        ];
        FieldInfo[] fields = typeof(ImageViewerWindow).GetFields(
            BindingFlags.Instance
            | BindingFlags.NonPublic);

        fields
            .Select(field => field.FieldType)
            .Should()
            .NotContain(
                fieldType => forbiddenDependencies.Any(
                    forbiddenDependency =>
                    forbiddenDependency.IsAssignableFrom(fieldType)));
    }

    [Fact]
    public void ImageViewerWindow_WhenInspected_OwnsInteractionCompositionInsteadOfControllers()
    {
        FieldInfo[] fields = typeof(ImageViewerWindow).GetFields(
            BindingFlags.Instance
            | BindingFlags.NonPublic
            | BindingFlags.DeclaredOnly);

        fields
            .Select(field => field.FieldType)
            .Should()
            .ContainSingle(
                fieldType => fieldType
                    == typeof(ImageViewerWindowInteractionComposition));
        fields
            .Where(field => field.FieldType.Name.EndsWith(
                "Controller",
                StringComparison.Ordinal))
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void WindowInfrastructure_WhenInspected_HasNoAttachOrDetachProtocol()
    {
        Type[] infrastructureTypes =
        [
            typeof(ImagePresentationController),
            typeof(ViewerAnimationFrameScheduler),
            typeof(ViewerWindowPlacementProvider),
            typeof(AvaloniaViewerFilePickerService),
            typeof(AvaloniaClipboardDataWriter)
        ];
        string[] forbiddenMethodNames = ["Attach", "Detach"];

        foreach (Type infrastructureType in infrastructureTypes)
        {
            string[] methodNames = infrastructureType
                .GetMethods(
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly)
                .Select(method => method.Name)
                .ToArray();

            methodNames.Should().NotIntersectWith(
                forbiddenMethodNames);
        }
    }

    [Fact]
    public void ImageViewerWindowFactory_WhenInspected_DelegatesComposition()
    {
        ConstructorInfo constructor = typeof(ImageViewerWindowFactory)
            .GetConstructors()
            .Should()
            .ContainSingle()
            .Subject;
        Type[] parameterTypes = constructor
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        parameterTypes.Should().Equal(
            typeof(IImageViewerStateService),
            typeof(IViewerUiDispatcher),
            typeof(ImageViewerWindowComposer));
    }
}
