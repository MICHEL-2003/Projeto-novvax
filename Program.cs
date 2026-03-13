using System.Text;
using System.Text.Json;
using System.Net;
using System.Net.Mail;
using System.Net.Http.Headers;
using Microsoft.VisualBasic;

namespace CSharpAsanaApp
{
    public class HistoricoEvento
    {
        public DateTime Data { get; set; } = DateTime.Now;
        public string Tipo { get; set; } = "";
        public int Versao { get; set; }
        public string? Comentario { get; set; }
        public string SnapshotDescricao { get; set; } = "";
        public string SnapshotPrazo { get; set; } = "";
    }

    public class Solicitacao
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string TokenAprovacao { get; set; } = Guid.NewGuid().ToString("N");
        public string TokenRenegociacao { get; set; } = Guid.NewGuid().ToString("N");

        public string Titulo { get; set; } = "";
        public string Setor { get; set; } = "";
        public string Descricao { get; set; } = "";
        public string Prazo { get; set; } = "";
        public string Responsavel { get; set; } = "";
        public string EmailSolicitante { get; set; } = "";
        public string EmailResponsavel { get; set; } = "";
        public string Status { get; set; } = "Pendente";
        public int Versao { get; set; } = 1;

        public List<HistoricoEvento> Historico { get; set; } = new();
    
        public static class DiretorioUsuarios
{
        public static Dictionary<string, string> Emails = new()
    {
        { "giovana", "giovanna.oliveira@novvax.com.br" },
        { "renato", "renato.grandchamp@novvax.com.br" },
        { "ana", "ana.silva@inoltra.tech" },
        { "marcelo", "marcelo.grandchamp@inoltra.tech" },
        { "ramis", "ramis.damasceno@inoltra.tech" }, 
        {"michel", "michel.brito@novvax.com.br"}, 
        {"guilherme", "sousaguilhermejorge@gmail.com"}
    };
}   
    
    }

    public class Program
    {
        private static readonly object _fileLock = new object();

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
            builder.WebHost.UseUrls($"http://*:{port}");
            
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.SetMinimumLevel(LogLevel.Debug);

            builder.Configuration
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", false, true);

            var app = builder.Build();
            var config = app.Configuration;
            var file = "solicitacoes.json";

            app.UseDefaultFiles();
            app.UseStaticFiles();

            // =========================
            // ENVIAR
            // =========================

            app.MapPost("/api/enviar", async ctx =>
            {
                var logger = ctx.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("API_ENVIAR");

                logger.LogDebug("Início do endpoint /api/enviar");

                try
                {
                    logger.LogDebug("Lendo formulário da requisição");
                    var f = await ctx.Request.ReadFormAsync();

                    logger.LogDebug("Dados recebidos: titulo={Titulo}, setor={Setor}, responsavel={Responsavel}",
                        f["titulo"].ToString(),
                        f["setor"].ToString(),
                        f["responsavel"].ToString()
                    );

                    var responsavelKey = f["responsavel"].ToString().ToLower();

                    if (!Solicitacao.DiretorioUsuarios.Emails.TryGetValue(responsavelKey, out var emailResponsavel))
                    {
                        ctx.Response.StatusCode = 400;
                        await ctx.Response.WriteAsync("Responsável inválido");
                        return;
                    }

                    var s = new Solicitacao
                    {
                        Titulo = f["titulo"].ToString(),
                        Setor = f["setor"].ToString(),
                        Responsavel = responsavelKey,
                        Descricao = f["descricao"].ToString(),
                        Prazo = f["prazo"].ToString(),
                        EmailSolicitante = f["Solicitante_email"].ToString(),
                        EmailResponsavel = emailResponsavel
                    };

                    logger.LogDebug("Solicitação criada: {@Solicitacao}", s);

                    s.Historico.Add(new HistoricoEvento
                    {
                        Tipo = "Envio inicial",
                        Versao = s.Versao,
                        SnapshotDescricao = s.Descricao,
                        SnapshotPrazo = s.Prazo
                    });

                 logger.LogDebug("Histórico inicial adicionado");

                    logger.LogDebug("Carregando lista do arquivo");
                    var lista = Load(file);

                    logger.LogDebug("Lista carregada. Total antes: {Total}", lista.Count);

                    lista.Add(s);

                    logger.LogDebug("Salvando lista no arquivo");
                    Save(file, lista);

                    logger.LogDebug("Arquivo salvo com sucesso. Total agora: {Total}", lista.Count);

                    logger.LogDebug("Enviando email de aprovação");
                    EnviarEmailAprovacao(ctx, config, s);

                    logger.LogDebug("Email enviado com sucesso");
                   
                    logger.LogDebug("Redirect para /sucesso.html realizado");

                    ctx.Response.Redirect("/sucesso.html");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());

                    ctx.Response.StatusCode = 500;
                    await ctx.Response.WriteAsync(ex.ToString());
                }
            });

        app.MapPost("/api/desistir", async ctx =>
        {
            var body = await JsonSerializer.DeserializeAsync<JsonElement>(ctx.Request.Body);
            var token = body.GetProperty("token").GetString();
            Console.WriteLine("===============");
            Console.WriteLine("TOKEN RECEBIDO: " + token);
            Console.WriteLine("===============");

            var lista = Load(file);

            // Procura pelos dois tipos de token
            var s = lista.FirstOrDefault(x =>
                x.TokenRenegociacao == token ||
                x.TokenAprovacao == token
            );

            if (s == null)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("Solicitação não encontrada");
                return;
            }

            s.Status = "Desistido";

            s.Historico.Add(new HistoricoEvento
            {
                Tipo = "Desistência",
                Versao = s.Versao,
                SnapshotDescricao = s.Descricao,
                SnapshotPrazo = s.Prazo
            });

            Save(file, lista);

            // 🔎 Se o token for de aprovação → foi o RESPONSÁVEL que desistiu
            Console.WriteLine("Token recebido: " + token);
            Console.WriteLine("TokenAprovacao salvo: " + s.TokenAprovacao);
            Console.WriteLine("TokenRenegociacao salvo: " + s.TokenRenegociacao);
            Console.WriteLine("Email solicitante: " + s.EmailSolicitante);

             if (!string.IsNullOrWhiteSpace(token) &&
            s.TokenAprovacao.Equals(token.Trim(), StringComparison.Ordinal))
    {
        EnviarEmail(
            config,
            s.EmailSolicitante, // envia para o solicitante
            $"🚫 Responsável desistiu: {s.Titulo}",
            $@"O responsável desistiu da solicitação.

            Título: {s.Titulo}
            Setor: {s.Setor}
            Versão: {s.Versao}

            Status final: DESISTIDO"
                    );
                }
            else
            {
        // 🔎 Se for token renegociação → foi o solicitante
        EnviarEmail(
            config,
            s.EmailResponsavel,  // envia para o responsável
            $"🚫 Solicitante desistiu: {s.Titulo}",
            $@"O solicitante desistiu da solicitação.

            Título: {s.Titulo}
            Setor: {s.Setor}
            Versão: {s.Versao}

            Status final: DESISTIDO"
                    );
                }

                await ctx.Response.WriteAsync("OK");
});

            // =========================
            // REPROVAR
            // =========================
            app.MapPost("/api/decidir", async ctx =>
            {
                var body = await JsonSerializer.DeserializeAsync<JsonElement>(ctx.Request.Body);
                var token = body.GetProperty("token").GetString();
                var status = body.GetProperty("status").GetString()!;
                var comentario = body.TryGetProperty("comentario", out var c) ? c.GetString() : null;

                var lista = Load(file);
                var s = lista.FirstOrDefault(x => x.TokenAprovacao == token);

                if (s == null || s.Status != "Pendente")
                {
                    ctx.Response.StatusCode = 400;
                    return;
                }
                if (status == "Desistir")
                {
                    s.Status = "Desistido";

                    EnviarEmail(
                        config,
                        s.EmailSolicitante,
                        $"🚫 O responsável desistiu da sua solicitação: {s.Titulo}",
                        $@"Ocorreu a desistência da solicitação.

                Título: {s.Titulo}
                Setor: {s.Setor}
                Versão: {s.Versao}

                Status final: DESISTIDO"
                    );

                    Save(file, lista);
                    await ctx.Response.WriteAsync("OK");
                    return;
                }
                if (status.StartsWith("Reprov"))
                {
                    s.Status = "Reprovado";
                    s.TokenRenegociacao = Guid.NewGuid().ToString("N");

                    s.Historico.Add(new HistoricoEvento
                    {
                        Tipo = "Reprovado",
                        Versao = s.Versao,
                        Comentario = comentario,
                        SnapshotDescricao = s.Descricao,
                        SnapshotPrazo = s.Prazo
                    });

                    var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
                    var link = $"{baseUrl}/renegociar.html?token={s.TokenRenegociacao}";

                    EnviarEmail(config, s.EmailSolicitante,
                        $"❌ Solicitação reprovada: {s.Titulo}",
                        $@"Motivo:
                        {comentario}

                        Renegociar:
                        {link}"
                    );
                }

                if (status.StartsWith("Aprov"))
                {
                    s.Status = "Aprovado";

                    s.Historico.Add(new HistoricoEvento
                    {
                        Tipo = "Aprovado",
                        Versao = s.Versao,
                        Comentario = comentario,
                        SnapshotDescricao = s.Descricao,
                        SnapshotPrazo = s.Prazo
                    });
                    EnviarEmail(
                        config,
                        s.EmailSolicitante,
                    $"✅ Solicitação aprovada: {s.Titulo}",
                    $@"Sua solicitação foi APROVADA pelo responsável.
                    Título: {s.Titulo}
                    Setor: {s.Setor}
                    Versão: {s.Versao}
                    Status atual: APROVADO"
                    );
                    CriarTaskAsana(config, s);
                }

                await ctx.Response.WriteAsync("OK");
                
                Save(file, lista);
                await ctx.Response.WriteAsync("OK");
            });

            // =========================
            // RENEGOCIAR
            // =========================
            app.MapPost("/api/aprovar-renegociacao", async ctx =>
            {
                var body = await JsonSerializer.DeserializeAsync<JsonElement>(ctx.Request.Body);
                var token = body.GetProperty("token").GetString();

                var lista = Load(file);
                var s = lista.FirstOrDefault(x => x.TokenRenegociacao == token);

                if (s == null)
                {
                    ctx.Response.StatusCode = 400;
                    await ctx.Response.WriteAsync("Solicitação não encontrada");
                    return;
                }
                s.Historico.Add(new HistoricoEvento
                {
                    Tipo = "Aprovação",
                    Versao = s.Versao,
                    SnapshotDescricao = s.Descricao,
                    SnapshotPrazo = s.Prazo
                });

                Save(file, lista);

                EnviarEmail(
                    config,
                    s.EmailSolicitante,
                    $"Solicitação aprovada: {s.Titulo}",
                    $@"Sua solicitação foi aprovada!,

                    
                    Título: {s.Titulo}
                    Setor:  {s.Setor}
                    Versão: {s.Versao}    

                    Status atual: APROVADO"
                );

                CriarTaskAsana(config, s);

                await ctx.Response.WriteAsync("OK");

            });

            app.MapPost("/api/reprovar-renegociacao", async ctx =>
{
                var body = await JsonSerializer.DeserializeAsync<JsonElement>(ctx.Request.Body);
                var token = body.GetProperty("token").GetString();
                var comentario = body.GetProperty("comentario").GetString();
                var novoPrazo = body.TryGetProperty("prazo", out var p) ? p.GetString() : null;

                var lista = Load(file);
                var s = lista.FirstOrDefault(x => x.TokenRenegociacao == token);

                if (s == null)
                {
                    ctx.Response.StatusCode = 400;
                    await ctx.Response.WriteAsync("Solicitação não encontrada");
                    return;
                }

                // Atualiza versão e status
                s.Versao++;
                s.Status = "Pendente";
                //s.Descricao = comentario;
                if (!string.IsNullOrEmpty(novoPrazo))
                    s.Prazo = novoPrazo;

                // Gera NOVO token de aprovação para o responsável
                s.TokenAprovacao = Guid.NewGuid().ToString("N");

                // Adiciona histórico
                s.Historico.Add(new HistoricoEvento
                {
                    Tipo = "Contra-proposta do solicitante",
                    Versao = s.Versao,
                    Comentario = comentario,
                    //SnapshotDescricao = s.Descricao,
                    SnapshotPrazo = s.Prazo
                });

                // Salva no arquivo
                Save(file, lista);

                // Monta link para o responsável avaliar
                var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
                var link = $"{baseUrl}/decidir.html?token={s.TokenAprovacao}";

                // Envia email para o responsável com link de decisão
                EnviarEmail(
                    config,
                    s.EmailResponsavel,
                    $"🔁 Contra-proposta: {s.Titulo} (v{s.Versao})", 
                    $@"O solicitante enviou uma CONTRA-PROPOSTA.

            Nova descrição:
            {s.Descricao}

            Novo prazo sugerido:
            {s.Prazo}

            Avaliar novamente:
            {link}"
                );

                await ctx.Response.WriteAsync("OK");
            });



            // =========================
            // ASANA
            // =========================
            // =========================
            // =========================
            // DESISTÊNCIA
            // =========================
            static void CriarTaskAsanaDesistencia(IConfiguration config, Solicitacao s)
            {
                var asana = config.GetSection("Asana");

                var sb = new StringBuilder();
                sb.AppendLine(" SOLICITAÇÃO DE DESISTENCIA\n");
                sb.AppendLine($"Título: {s.Titulo}");
                sb.AppendLine($"Setor: {s.Setor}");
                sb.AppendLine($"Versão: {s.Versao}");
                sb.AppendLine($"Status final: DESISTIDO\n");
                sb.AppendLine("📜 HISTÓRICO COMPLETO\n");

                foreach (var h in s.Historico)
                {
                    sb.AppendLine($"[{h.Data:dd/MM HH:mm}] {h.Tipo} (v{h.Versao})");

                    if (!string.IsNullOrWhiteSpace(h.Comentario))
                        sb.AppendLine($"Comentário: {h.Comentario}");

                    sb.AppendLine($"Descrição: {h.SnapshotDescricao}");
                    sb.AppendLine($"Prazo: {h.SnapshotPrazo}");
                    sb.AppendLine("----------------------");
                }

                using var http = new HttpClient();
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", asana["Token"]);

                var payload = new
                {
                    data = new
                    {
                        name = $"🚫 DESISTÊNCIA — {s.Titulo}",
                        notes = sb.ToString(),
                        projects = new[] { asana["ProjetoUnico"] }
                    }
                };

                http.PostAsync(
                    "https://app.asana.com/api/1.0/tasks",
                    new StringContent(
                        JsonSerializer.Serialize(payload),
                        Encoding.UTF8,
                        "application/json"
                    )
                ).Wait();
            }
            //===================
            //===================
            // APROVAR    
            //===================
            //===================       
            static void CriarTaskAsana(IConfiguration config, Solicitacao s)
{
            var asana = config.GetSection("Asana");

            var sb = new StringBuilder();
            sb.AppendLine("📜 HISTÓRICO COMPLETO\n");

            foreach (var h in s.Historico)
            {
                sb.AppendLine($"[{h.Data:dd/MM HH:mm}] {h.Tipo} (v{h.Versao})");
                if (!string.IsNullOrWhiteSpace(h.Comentario))
                    sb.AppendLine($"Comentário: {h.Comentario}");
                sb.AppendLine($"Descrição: {h.SnapshotDescricao}");
                sb.AppendLine($"Prazo: {h.SnapshotPrazo}");
                sb.AppendLine("----------------------");
            }

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", asana["Token"]);

            // 🔹 Separar parsing da data
            string? due_on = null;
            if (DateTime.TryParse(s.Prazo, out var dt))
            {
                due_on = dt.ToString("yyyy-MM-dd");
            }

            // 🔹 Criar payload corretamente
            var payload = new
            {
                data = new
                {
                    name = $"✅ APROVADO — {s.Titulo}",
                    notes = sb.ToString(),
                    projects = new[] { asana["ProjetoUnico"] },
                    due_on,                       // usa a variável definida acima
                    assignee = s.EmailResponsavel  // email do responsável
                }
            };

            // 🔹 Enviar para Asana
            http.PostAsync(
                "https://app.asana.com/api/1.0/tasks",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            ).Wait();
}
            // =========================
            // HELPERS
            // =========================
            static List<Solicitacao> Load(string f) =>
                File.Exists(f) ? JsonSerializer.Deserialize<List<Solicitacao>>(File.ReadAllText(f)) ?? new() : new();

            static void Save(string f, List<Solicitacao> l) =>
                File.WriteAllText(f, JsonSerializer.Serialize(l, new JsonSerializerOptions { WriteIndented = true }));

            static void EnviarEmailAprovacao(HttpContext ctx, IConfiguration config, Solicitacao s)
            {
                var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
                var link = $"{baseUrl}/decidir.html?token={s.TokenAprovacao}";

                EnviarEmail(config, s.EmailResponsavel,
                    $"[Solicitação] {s.Titulo} (v{s.Versao})",
                    $@"Descrição:
                {s.Descricao}

                Aprovar / Reprovar:
                {link}",
                s.EmailSolicitante
                );
            }

            static void EnviarEmail(IConfiguration c, string to, string subject, string body, string? replyTo = null)
            {
                var e = c.GetSection("Email");

                var smtp = new SmtpClient(e["Host"])
                {
                    Port = int.Parse(e["Port"]!),
                    EnableSsl = bool.Parse(e["EnableSsl"]!),
                    Credentials = new NetworkCredential(e["User"], e["Password"])
                };

                var msg = new MailMessage(e["From"]!, to, subject, body);

                if (!string.IsNullOrWhiteSpace(replyTo))
                    msg.ReplyToList.Add(replyTo);

                smtp.Send(msg);
            }
            app.Run();
        }
    }
}