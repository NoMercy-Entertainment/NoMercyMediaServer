namespace NoMercy.Api.DTOs.Media;

public class ComponentPropsBuilder<T>
{
    private readonly RenderProps<T> _props;

    public ComponentPropsBuilder(RenderProps<T> props)
    {
        _props = props;
    }

    public ComponentPropsBuilder<T> WithId(dynamic id)
    {
        _props.Id = id;
        return this;
    }

    public ComponentPropsBuilder<T> WithNextId(dynamic nextId)
    {
        _props.NextId = nextId;
        return this;
    }

    public ComponentPropsBuilder<T> WithPreviousId(dynamic previousId)
    {
        _props.PreviousId = previousId;
        return this;
    }

    public ComponentPropsBuilder<T> WithTitle(string title)
    {
        _props.Title = title;
        return this;
    }

    public ComponentPropsBuilder<T> WithMoreLink(Uri? moreLink)
    {
        _props.MoreLink = moreLink;
        return this;
    }

    public ComponentPropsBuilder<T> WithData(T data)
    {
        _props.Data = data;
        return this;
    }

    public ComponentPropsBuilder<T> WithWatch(bool watch = true)
    {
        _props.Watch = watch;
        
        return this;
    }

    public ComponentPropsBuilder<T> WithContextMenuItems(Dictionary<string, object>[]? items)
    {
        _props.ContextMenuItems = items;
        return this;
    }

    public ComponentPropsBuilder<T> WithItems<TChild>(IEnumerable<ComponentDto<TChild>> items)
    {
        _props.Items = items.Cast<ComponentDto<T>>();
        return this;
    }

    public void WithItems<T1>(IEnumerable<T1> selectMany)
    {
        _props.Items = selectMany.Cast<ComponentDto<T>>();
    }

    public ComponentPropsBuilder<T> WithProperties(Dictionary<string, dynamic> o)
    {
        _props.Properties = o;
        
        return this;
    }
}