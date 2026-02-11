namespace NoMercy.Api.DTOs.Media;

public class ComponentBuilder<T>
{
    private readonly ComponentDto<T> _component = new();

    public ComponentBuilder<T> WithComponent(string componentName)
    {
        _component.Component = componentName;
        return this;
    }

    public ComponentBuilder<T> WithUpdate(string? when = null, string? link = null)
    {
        if (when is not null) _component.Update.When = when;
        if (link is not null) _component.Update.Link = new(link, UriKind.Relative);
        return this;
    }

    public ComponentBuilder<T> WithProps(Action<ComponentPropsBuilder<T>, Ulid> propsBuilder)
    {
        ComponentPropsBuilder<T> builder = new(_component.Props);
        propsBuilder(builder, _component.Id);
        return this;
    }

    public ComponentBuilder<T> WithId(Ulid id)
    {
        _component.Id = id;
        return this;
    }

    public ComponentBuilder<T> WithReplacing(Ulid replacingId)
    {
        _component.Replacing = replacingId;
        return this;
    }

    public ComponentDto<T> Build()
    {
        return _component;
    }
}