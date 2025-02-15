using System.Buffers;
using MessagePack;
using Stl.Serialization.Internal;

namespace Stl.Serialization;

public class MessagePackByteSerializer(MessagePackSerializerOptions options) : IByteSerializer
{
    private readonly ConcurrentDictionary<Type, MessagePackByteSerializer> _typedSerializers = new();

    public static IFormatterResolver DefaultResolver { get; set; } = DefaultMessagePackResolver.Instance;
    public static MessagePackSerializerOptions DefaultOptions { get; set; } = new(DefaultResolver);
    public static MessagePackByteSerializer Default { get; set; } = new(DefaultOptions);

    public MessagePackSerializerOptions Options { get; } = options;

    public MessagePackByteSerializer() : this(DefaultOptions) { }

    public IByteSerializer<T> ToTyped<T>(Type? serializedType = null)
        => (IByteSerializer<T>) GetTypedSerializer(serializedType ?? typeof(T));

    public virtual object? Read(ReadOnlyMemory<byte> data, Type type, out int readLength)
    {
        var serializer = _typedSerializers.GetOrAdd(type,
            static (type1, self) => (MessagePackByteSerializer)typeof(MessagePackByteSerializer<>)
                .MakeGenericType(type1)
                .CreateInstance(self.Options, type1),
            this);
        return serializer.Read(data, type, out readLength);
    }

    public virtual void Write(IBufferWriter<byte> bufferWriter, object? value, Type type)
    {
        var serializer = _typedSerializers.GetOrAdd(type,
            static (type1, self) => (MessagePackByteSerializer)typeof(MessagePackByteSerializer<>)
                .MakeGenericType(type1)
                .CreateInstance(self.Options, type1),
            this);
        serializer.Write(bufferWriter, value, type);
    }

    // Private methods

    private MessagePackByteSerializer GetTypedSerializer(Type serializedType)
        => _typedSerializers.GetOrAdd(serializedType,
            static (type1, self) => (MessagePackByteSerializer)typeof(MessagePackByteSerializer<>)
                .MakeGenericType(type1)
                .CreateInstance(self.Options, type1),
            this);
}

public class MessagePackByteSerializer<T>(MessagePackSerializerOptions options, Type serializedType)
    : MessagePackByteSerializer(options), IByteSerializer<T>
{
    public Type SerializedType { get; } = serializedType;

    public override object? Read(ReadOnlyMemory<byte> data, Type type, out int readLength)
    {
        if (type != SerializedType)
            throw Errors.SerializedTypeMismatch(SerializedType, type);

        // ReSharper disable once HeapView.PossibleBoxingAllocation
        return Read(data, out readLength);
    }

    public override void Write(IBufferWriter<byte> bufferWriter, object? value, Type type)
    {
        if (type != SerializedType)
            throw Errors.SerializedTypeMismatch(SerializedType, type);

        Write(bufferWriter, (T)value!);
    }

    public T Read(ReadOnlyMemory<byte> data, out int readLength)
        => MessagePackSerializer.Deserialize<T>(data, Options, out readLength);

    public void Write(IBufferWriter<byte> bufferWriter, T value)
        => MessagePackSerializer.Serialize(bufferWriter, value, Options);
}
