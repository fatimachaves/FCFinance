using FCFinance.Models;
using FCFinance.Services;
using FCFinance.Services.ChatBot;
using Microsoft.EntityFrameworkCore;
using ValueType = FCFinance.Models.ValueType;

namespace FCFinance.Tests
{
    // Relógio controlado pelo teste (para simular o tempo passando).
    public class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 21, 15, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    public class ChatBotServiceTests : IDisposable
    {
        private const string Phone = "5511999998888";
        private readonly TestDb _db = new();
        private readonly TestClock _clock = new();

        public ChatBotServiceTests()
        {
            using var ctx = _db.NewContext();
            ctx.Categories.AddRange(
                new Category { Name = "Mercado", TypeId = ValueType.Despesa },
                new Category { Name = "Salário", TypeId = ValueType.Receita },
                new Category { Name = "Pet", TypeId = ValueType.Despesa, UserId = Users.Ana },
                new Category { Name = "Pet da Bia", TypeId = ValueType.Despesa, UserId = Users.Bia });
            ctx.SaveChanges();
        }

        public void Dispose() => _db.Dispose();

        // Cada mensagem usa um contexto novo, como uma requisição nova na API.
        private ChatBotService NewBot()
        {
            var ctx = _db.NewContext();
            return new ChatBotService(ctx, new RegisteredValueService(ctx), _clock, TimeZoneInfo.Utc);
        }

        // Conversa no simulador (usuário já conhecido).
        private Task<string> Say(string text, string user = Users.Ana) =>
            NewBot().HandleAsync($"sim:{user}", text, simulatedUserId: user);

        private List<RegisteredValue> Saved()
        {
            using var ctx = _db.NewContext();
            return ctx.RegisteredValues.Include(r => r.Category).ToList();
        }

        [Fact]
        public async Task Conversa_completa_registra_a_despesa()
        {
            Assert.Contains("R$ 45,90", await Say("45,90"));
            var categorias = await Say("2");
            Assert.Contains("Mercado", categorias);
            Assert.Contains("Pet", categorias);
            Assert.DoesNotContain("Salário", categorias);   // só categorias de despesa
            Assert.DoesNotContain("Pet da Bia", categorias); // nem categorias de outro usuário

            Assert.Contains("descrição", await Say("1"));   // 1 = Mercado (do sistema vem primeiro)
            var fim = await Say("Compras da semana");

            Assert.Contains("Despesa registrada", fim);
            var item = Assert.Single(Saved());
            Assert.Equal(45.90m, item.Value);
            Assert.Equal(ValueType.Despesa, item.ValueTypeId);
            Assert.Equal("Mercado", item.Category.Name);
            Assert.Equal("Compras da semana", item.Name);
            Assert.Equal(Users.Ana, item.UserId);
            Assert.Equal(new DateTime(2026, 11, 21), item.CreateWhen);
        }

        [Fact]
        public async Task Sem_descricao_usa_o_nome_da_categoria_e_aceita_categoria_pelo_nome()
        {
            await Say("R$ 5.190,00");
            await Say("receita");
            await Say("salario");   // pelo nome, sem acento
            await Say("Não");

            var item = Assert.Single(Saved());
            Assert.Equal(5190m, item.Value);
            Assert.Equal(ValueType.Receita, item.ValueTypeId);
            Assert.Equal("Salário", item.Name);
        }

        [Fact]
        public async Task Respostas_invalidas_repetem_a_pergunta()
        {
            Assert.Contains("Não entendi", await Say("abc"));
            await Say("10");
            Assert.Contains("Responda *1*", await Say("talvez"));
            await Say("2");
            Assert.Contains("Não encontrei", await Say("99"));
            Assert.Empty(Saved());
        }

        [Fact]
        public async Task Cancelar_para_a_conversa()
        {
            await Say("10");
            await Say("2");
            Assert.Contains("cancelado", await Say("Cancelar"));
            Assert.Contains("Valor: *R$ 20,00*", await Say("20")); // recomeça do valor
            Assert.Empty(Saved());
        }

        [Fact]
        public async Task Conversa_parada_por_mais_de_10_minutos_recomeca()
        {
            await Say("10");
            _clock.Now = _clock.Now.AddMinutes(11);

            var reply = await Say("2"); // "2" agora é tratado como um valor novo
            Assert.Contains("foi cancelado", reply);
            Assert.Contains("Valor: *R$ 2,00*", reply);
        }

        [Fact]
        public async Task Telefone_desconhecido_precisa_do_codigo_do_site()
        {
            Assert.Contains("conecte seu WhatsApp", await NewBot().HandleAsync(Phone, "50"));

            var code = await NewBot().CreateLinkCodeAsync(Users.Ana);
            Assert.Contains("conectado", await NewBot().HandleAsync(Phone, code.Code));

            // Agora o telefone é da Ana.
            await NewBot().HandleAsync(Phone, "50");
            await NewBot().HandleAsync(Phone, "2");
            await NewBot().HandleAsync(Phone, "2"); // 2 = Pet
            await NewBot().HandleAsync(Phone, "nao");
            var item = Assert.Single(Saved());
            Assert.Equal(Users.Ana, item.UserId);
            Assert.Equal("Pet", item.Name);
        }

        [Fact]
        public async Task Codigo_vencido_nao_conecta()
        {
            var code = await NewBot().CreateLinkCodeAsync(Users.Ana);
            _clock.Now = _clock.Now.Add(ChatBotService.LinkCodeLifetime).AddSeconds(1);

            Assert.Contains("inválido ou vencido", await NewBot().HandleAsync(Phone, code.Code));
        }

        [Fact]
        public async Task Muitos_codigos_errados_bloqueiam_o_telefone()
        {
            var code = await NewBot().CreateLinkCodeAsync(Users.Ana);
            var wrong = code.Code == "000000" ? "111111" : "000000";
            for (var i = 0; i < 5; i++) await NewBot().HandleAsync(Phone, wrong);

            // Mesmo o código certo é recusado durante o bloqueio.
            Assert.Contains("Muitas tentativas", await NewBot().HandleAsync(Phone, code.Code));

            _clock.Now = _clock.Now.AddHours(1).AddMinutes(1);
            Assert.Contains("conectado", await NewBot().HandleAsync(Phone, (await NewBot().CreateLinkCodeAsync(Users.Ana)).Code));
        }

        [Fact]
        public async Task Telefone_ja_conectado_trata_6_digitos_como_valor()
        {
            await NewBot().HandleAsync(Phone, (await NewBot().CreateLinkCodeAsync(Users.Ana)).Code);
            var outroCodigo = await NewBot().CreateLinkCodeAsync(Users.Bia);

            var reply = await NewBot().HandleAsync(Phone, outroCodigo.Code);

            Assert.Contains("Valor:", reply); // não troca de conta
            using var ctx = _db.NewContext();
            Assert.Equal(Users.Ana, ctx.WhatsAppLinks.Single(l => l.PhoneNumber == Phone).UserId);
        }

        [Theory]
        [InlineData("45", 45)]
        [InlineData("45,9", 45.9)]
        [InlineData("45,90", 45.90)]
        [InlineData("1.234,56", 1234.56)]
        [InlineData("1234.56", 1234.56)]
        [InlineData("1.500", 1500)]
        [InlineData("R$ 50", 50)]
        [InlineData("r$1.500,00", 1500)]
        public void Le_valores_em_reais(string text, double expected) =>
            Assert.Equal((decimal)expected, ChatText.ParseMoney(text));

        [Theory]
        [InlineData("abc")]
        [InlineData("")]
        [InlineData("1,234,56")]
        [InlineData("10,999")]
        [InlineData("-5")]
        public void Recusa_textos_que_nao_sao_valor(string text) =>
            Assert.Null(ChatText.ParseMoney(text));
    }
}
