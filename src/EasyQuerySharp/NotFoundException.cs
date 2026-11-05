namespace EasyQuerySharp;

/// <summary>
/// 要求有结果的查询没有查到行（对应 Java 的 firstNotNull / singleNotNull / findNotNull / required 抛出的异常）。
/// 默认消息取根实体上 <see cref="Metadata.TableAttribute.NotFoundMessage"/> 的配置；接口层通常据此回 404。
/// </summary>
public sealed class NotFoundException(string message) : InvalidOperationException(message);
