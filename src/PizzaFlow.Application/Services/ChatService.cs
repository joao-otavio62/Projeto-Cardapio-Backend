using PizzaFlow.Application.Abstractions.Repositories;
using PizzaFlow.Application.Abstractions.Services;
using PizzaFlow.Application.DTOs;
using PizzaFlow.Domain.Exceptions;

namespace PizzaFlow.Application.Services;

public class ChatService(IStoreRepository stores, IChatAssistant assistant)
{
    public async Task<ChatResponse> ReplyAsync(string slug, ChatRequest request, CancellationToken ct)
    {
        var store = await stores.GetBySlugAsync(slug, ct) ?? throw new NotFoundException("Loja não encontrada.");

        // só as últimas mensagens e só os papéis permitidos (limita custo e superfície de prompt injection)
        var history = request.Messages
            .Where(m => m.Role is "user" or "assistant")
            .TakeLast(10)
            .Select(m => new ChatMessageDto(m.Role, m.Content.Length > 1000 ? m.Content[..1000] : m.Content))
            .ToList();

        var reply = await assistant.ReplyAsync(new ChatContext(store.Slug, store.Name), history, ct);
        return new ChatResponse(reply);
    }
}
