using System.Security.Cryptography;
using FCFinance.DTO;
using FCFinance.Models;
using Microsoft.EntityFrameworkCore;
using ValueType = FCFinance.Models.ValueType;

namespace FCFinance.Services.ChatBot
{
    // "Cérebro" do bot: recebe o texto de uma mensagem e devolve a resposta.
    // Não sabe nada do WhatsApp em si (quem envia/recebe é o WhatsAppController),
    // por isso o mesmo código atende o WhatsApp de verdade e o simulador do site.
    //
    // Conversa:  valor  ->  receita ou despesa?  ->  categoria?  ->  descrição?  ->  salva
    // "cancelar" para a qualquer momento; sem resposta por 10 minutos, a conversa recomeça.
    public class ChatBotService
    {
        public static readonly TimeSpan SessionTimeout = TimeSpan.FromMinutes(10);
        public static readonly TimeSpan LinkCodeLifetime = TimeSpan.FromMinutes(10);
        private const int MaxFailedCodes = 5;
        private static readonly TimeSpan FailedCodeWindow = TimeSpan.FromHours(1);
        private const decimal MaxAmount = 999_999_999_999m;

        private static readonly string[] CancelWords = ["cancelar", "cancela", "cancel", "sair", "parar"];
        private static readonly string[] HelpWords = ["ajuda", "help", "menu", "oi", "ola", "bom dia", "boa tarde", "boa noite", "inicio", "comecar"];
        private static readonly string[] IncomeWords = ["1", "receita", "r", "entrada", "ganho"];
        private static readonly string[] ExpenseWords = ["2", "despesa", "d", "saida", "gasto"];
        private static readonly string[] SkipWords = ["nao", "n", "pular", "sem", "sem descricao", "nenhuma", "0"];

        private readonly AppDbContext _db;
        private readonly RegisteredValueService _values;
        private readonly TimeProvider _clock;
        private readonly TimeZoneInfo _timeZone;

        public ChatBotService(AppDbContext db, RegisteredValueService values, TimeProvider clock, TimeZoneInfo timeZone)
        {
            _db = db;
            _values = values;
            _clock = clock;
            _timeZone = timeZone;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        // ================== Mensagens ==================

        // phone: número do WhatsApp (ou "sim:<userId>" no simulador).
        // simulatedUserId: no simulador o usuário já é conhecido (não precisa do código de ligação).
        public async Task<string> HandleAsync(string phone, string? text, string? simulatedUserId = null)
        {
            var input = (text ?? "").Trim();
            var cmd = ChatText.Normalize(input);
            var session = await _db.ChatSessions.FindAsync(phone);

            var userId = simulatedUserId ?? await _db.WhatsAppLinks
                .Where(l => l.PhoneNumber == phone).Select(l => l.UserId).SingleOrDefaultAsync();

            // Telefone ainda não conectado: só aceita o código de 6 números gerado no site.
            // (Para um telefone já conectado, "150000" é um valor, nunca um código.)
            if (userId == null)
            {
                if (cmd.Length != 6 || !cmd.All(char.IsDigit)) return Msg.NotLinked;
                return await TryLinkAsync(phone, cmd, session) ?? await FailedCodeAsync(phone, session);
            }

            if (session == null)
            {
                session = new ChatSession { PhoneNumber = phone };
                _db.ChatSessions.Add(session);
            }

            var expired = session.Step != ChatStep.Idle && Now - session.UpdatedAt > SessionTimeout;
            if (expired) Reset(session);

            string reply;
            if (CancelWords.Contains(cmd))
            {
                reply = session.Step == ChatStep.Idle ? Msg.NothingToCancel : Msg.Cancelled;
                Reset(session);
            }
            else
            {
                reply = session.Step switch
                {
                    ChatStep.AskType => await OnTypeAsync(session, userId, cmd),
                    ChatStep.AskCategory => await OnCategoryAsync(session, userId, cmd),
                    ChatStep.AskDescription => await OnDescriptionAsync(session, userId, input, cmd),
                    _ => OnAmount(session, input, cmd)
                };
            }

            session.UpdatedAt = Now;
            await _db.SaveChangesAsync();
            return expired ? Msg.Expired + reply : reply;
        }

        private static string OnAmount(ChatSession session, string input, string cmd)
        {
            if (HelpWords.Contains(cmd)) return Msg.Help;

            var amount = ChatText.ParseMoney(input);
            if (amount == null) return Msg.NotAnAmount;
            if (amount <= 0) return Msg.AmountMustBePositive;
            if (amount > MaxAmount) return Msg.AmountTooBig;

            session.Amount = amount;
            session.Step = ChatStep.AskType;
            return Msg.AskType(amount.Value);
        }

        private async Task<string> OnTypeAsync(ChatSession session, string userId, string cmd)
        {
            int? typeId = IncomeWords.Contains(cmd) ? ValueType.Receita
                        : ExpenseWords.Contains(cmd) ? ValueType.Despesa
                        : null;
            if (typeId == null) return Msg.InvalidType;

            var categories = await CategoriesAsync(userId, typeId.Value);
            if (categories.Count == 0)
            {
                Reset(session);
                return Msg.NoCategories(typeId.Value);
            }

            session.ValueTypeId = typeId;
            session.Step = ChatStep.AskCategory;
            return Msg.AskCategory(categories);
        }

        private async Task<string> OnCategoryAsync(ChatSession session, string userId, string cmd)
        {
            var categories = await CategoriesAsync(userId, session.ValueTypeId!.Value);

            Category? chosen = null;
            if (int.TryParse(cmd, out var number) && number >= 1 && number <= categories.Count)
                chosen = categories[number - 1];
            else if (cmd.Length > 0)
            {
                // Também aceita o nome (exato, ou um pedaço que só combine com uma categoria).
                chosen = categories.FirstOrDefault(c => ChatText.Normalize(c.Name) == cmd);
                if (chosen == null)
                {
                    var partial = categories.Where(c => ChatText.Normalize(c.Name).Contains(cmd)).ToList();
                    if (partial.Count == 1) chosen = partial[0];
                }
            }
            if (chosen == null) return Msg.InvalidCategory(categories);

            session.CategoryId = chosen.Id;
            session.Step = ChatStep.AskDescription;
            return Msg.AskDescription;
        }

        private async Task<string> OnDescriptionAsync(ChatSession session, string userId, string input, string cmd)
        {
            var category = await _values.CategoriesFor(userId)
                .SingleOrDefaultAsync(c => c.Id == session.CategoryId);
            if (category == null)
            {
                Reset(session);
                return Msg.CategoryGone;
            }

            string name;
            if (SkipWords.Contains(cmd) || input.Length == 0) name = category.Name;
            else if (input.Length > Limites.NomeLancamento) return Msg.DescriptionTooLong;
            else name = input;

            var today = TimeZoneInfo.ConvertTimeFromUtc(Now, _timeZone).Date;
            var dto = new RegisteredValueDTO
            {
                Name = name,
                Value = session.Amount!.Value,
                CategoryId = category.Id,
                ValueTypeId = session.ValueTypeId!.Value,
                CreateWhen = today
            };
            var (saved, error) = await _values.CreateAsync(userId, dto);
            Reset(session);

            return saved == null
                ? Msg.SaveFailed(error)
                : Msg.Saved(saved.ValueTypeId, saved.Value, category.Name, saved.Name, today);
        }

        private Task<List<Category>> CategoriesAsync(string userId, int typeId) =>
            _values.CategoriesFor(userId, typeId)
                .OrderBy(c => c.UserId != null) // do sistema primeiro, igual ao site
                .ThenBy(c => c.Name)
                .ToListAsync();

        private static void Reset(ChatSession session)
        {
            session.Step = ChatStep.Idle;
            session.Amount = null;
            session.ValueTypeId = null;
            session.CategoryId = null;
        }

        // ================== Ligação telefone <-> conta ==================

        // Gera o código que o usuário envia pelo WhatsApp. Invalida códigos anteriores dele.
        public async Task<WhatsAppLinkCode> CreateLinkCodeAsync(string userId)
        {
            await _db.WhatsAppLinkCodes
                .Where(c => c.UserId == userId || c.ExpiresAt < Now)
                .ExecuteDeleteAsync();

            string code;
            do code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            while (await _db.WhatsAppLinkCodes.AnyAsync(c => c.Code == code));

            var linkCode = new WhatsAppLinkCode { Code = code, UserId = userId, ExpiresAt = Now.Add(LinkCodeLifetime) };
            _db.WhatsAppLinkCodes.Add(linkCode);
            await _db.SaveChangesAsync();
            return linkCode;
        }

        public Task<WhatsAppLink?> GetLinkAsync(string userId) =>
            _db.WhatsAppLinks.SingleOrDefaultAsync(l => l.UserId == userId);

        public async Task UnlinkAsync(string userId)
        {
            var phones = await _db.WhatsAppLinks.Where(l => l.UserId == userId).Select(l => l.PhoneNumber).ToListAsync();
            await _db.WhatsAppLinks.Where(l => l.UserId == userId).ExecuteDeleteAsync();
            await _db.ChatSessions.Where(s => phones.Contains(s.PhoneNumber)).ExecuteDeleteAsync();
        }

        // Retorna a resposta se o código ligou o telefone (ou se está bloqueado); null se o código não existe.
        private async Task<string?> TryLinkAsync(string phone, string code, ChatSession? session)
        {
            if (IsBlocked(session)) return Msg.TooManyCodes;

            var linkCode = await _db.WhatsAppLinkCodes.SingleOrDefaultAsync(c => c.Code == code && c.ExpiresAt >= Now);
            if (linkCode == null) return null;

            // Um telefone por conta e uma conta por telefone: remove ligações antigas dos dois lados.
            await _db.WhatsAppLinks
                .Where(l => l.PhoneNumber == phone || l.UserId == linkCode.UserId)
                .ExecuteDeleteAsync();
            _db.WhatsAppLinks.Add(new WhatsAppLink { PhoneNumber = phone, UserId = linkCode.UserId, LinkedAt = Now });
            _db.WhatsAppLinkCodes.Remove(linkCode);

            if (session != null)
            {
                Reset(session);
                session.FailedCodeAttempts = 0;
                session.FailedCodeWindowStart = null;
                session.UpdatedAt = Now;
            }
            await _db.SaveChangesAsync();
            return Msg.Linked;
        }

        private async Task<string> FailedCodeAsync(string phone, ChatSession? session)
        {
            if (IsBlocked(session)) return Msg.TooManyCodes;
            if (session == null)
            {
                session = new ChatSession { PhoneNumber = phone };
                _db.ChatSessions.Add(session);
            }
            if (session.FailedCodeWindowStart == null || Now - session.FailedCodeWindowStart > FailedCodeWindow)
            {
                session.FailedCodeWindowStart = Now;
                session.FailedCodeAttempts = 0;
            }
            session.FailedCodeAttempts++;
            session.UpdatedAt = Now;
            await _db.SaveChangesAsync();
            return Msg.InvalidCode;
        }

        private bool IsBlocked(ChatSession? session) =>
            session?.FailedCodeWindowStart != null
            && Now - session.FailedCodeWindowStart <= FailedCodeWindow
            && session.FailedCodeAttempts >= MaxFailedCodes;

        // ================== Textos (em português: o app é para usuários brasileiros) ==================

        private static class Msg
        {
            public const string NotLinked =
                "Olá! 👋 Eu sou o assistente do *FCFinance*.\n\n" +
                "Para começar, conecte seu WhatsApp à sua conta:\n" +
                "1. No site, abra o menu do seu nome > *WhatsApp*\n" +
                "2. Clique em *Gerar código*\n" +
                "3. Envie aqui o código de 6 números.";

            public const string Linked =
                "✅ Pronto! Seu WhatsApp está conectado ao FCFinance.\n" +
                "(Para trocar de conta, desconecte no site e gere um novo código.)\n\n" +
                "Para registrar uma receita ou despesa, envie o *valor*. Ex.: *45,90*";

            public const string InvalidCode = "❌ Código inválido ou vencido. Gere um novo código no site e envie aqui.";
            public const string TooManyCodes = "⛔ Muitas tentativas com código errado. Aguarde 1 hora e tente de novo.";

            public const string Help =
                "Para registrar uma receita ou despesa, envie o *valor*. Ex.: *45,90*\n\n" +
                "Eu vou perguntar o tipo, a categoria e a descrição.\n" +
                "A qualquer momento, envie *cancelar* para parar.";

            public const string NotAnAmount = "Não entendi. 🤔 Envie o *valor* que quer registrar. Ex.: *45,90*\n(ou *ajuda*)";
            public const string AmountMustBePositive = "O valor precisa ser maior que zero. Envie o valor de novo.";
            public const string AmountTooBig = "Esse valor é grande demais. Envie o valor de novo.";

            public static string AskType(decimal amount) =>
                $"Valor: *{ChatText.Money(amount)}*\n\nÉ uma *receita* ou uma *despesa*?\n1️⃣ Receita\n2️⃣ Despesa";

            public const string InvalidType = "Não entendi. Responda *1* para Receita ou *2* para Despesa.";

            public static string NoCategories(int typeId) =>
                $"Você ainda não tem categorias de {(typeId == ValueType.Receita ? "receita" : "despesa")}. " +
                "Cadastre uma no site e envie o valor de novo.";

            public static string AskCategory(List<Category> categories) =>
                "Qual é a *categoria*? Responda com o número:\n\n" + List(categories);

            public static string InvalidCategory(List<Category> categories) =>
                "Não encontrei essa categoria. Responda com o *número* da lista:\n\n" + List(categories);

            private static string List(List<Category> categories) =>
                string.Join("\n", categories.Select((c, i) => $"*{i + 1}.* {c.Name}"));

            public const string AskDescription =
                "Quer adicionar uma *descrição*? ✏️\nEnvie o texto, ou *não* para pular.";

            public static readonly string DescriptionTooLong =
                $"A descrição pode ter no máximo {Limites.NomeLancamento} caracteres. Envie um texto menor, ou *não*.";

            public const string CategoryGone = "Essa categoria não existe mais. Vamos recomeçar: envie o valor.";

            public static string SaveFailed(string? error) =>
                $"❌ Não consegui salvar: {error}\nEnvie o valor para tentar de novo.";

            public static string Saved(int typeId, decimal amount, string category, string description, DateTime date) =>
                $"✅ {(typeId == ValueType.Receita ? "Receita" : "Despesa")} registrada!\n\n" +
                $"💰 {ChatText.Money(amount)}\n" +
                $"🏷️ {category}\n" +
                $"📝 {description}\n" +
                $"📅 {date:dd/MM/yyyy}\n\n" +
                "Para registrar outra, é só enviar o valor.";

            public const string Cancelled = "❌ Registro cancelado. Quando quiser, envie um valor.";
            public const string NothingToCancel = "Não há nenhum registro em andamento. Envie um valor para começar.";
            public const string Expired = "⏰ O registro anterior ficou parado mais de 10 minutos e foi cancelado.\n\n";
        }
    }
}
