using System.Runtime.CompilerServices;

// Тестам нужно дерево Huffman (internal), чтобы сжимать тестовый поток так же, как сервер (HuffmanTestEncoder).
[assembly: InternalsVisibleTo("TheVerge.Tests")]
