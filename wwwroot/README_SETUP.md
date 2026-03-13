## README_SETUP.md (instruções rápidas)


1) Instalar .NET 8 SDK
- Baixe e instale de https://dotnet.microsoft.com/download/dotnet/8.0 (escolha o SDK para sua plataforma Windows)


2) Criar a pasta do projeto
- Abra PowerShell ou terminal e crie uma pasta, ex: `C:\Apps\FormularioAsana`


3) Copie os arquivos do conteúdo deste repositório para a pasta criada


4) Atualize `appsettings.json` com seu `Asana:Token`, `Asana:ProjetoUnico` e IDs de tags (veja como obter abaixo)


5) Rodar localmente
- No terminal, execute:
```powershell
dotnet run
ou
dotnet run --project CSharpAsanaApp.csproj
```
- Por padrão o .NET irá expor a aplicação em `http://localhost:5000` e `http://localhost:5001` (HTTPS). Se quiser que fique acessível na rede, veja o passo de "tornar acessível na rede".


6) Tornar acessível na rede local
- Descubra o IP da máquina (por exemplo `192.168.0.50`)
- Permita no Firewall do Windows a porta 5000 (ou a porta usada)
- Se necessário, rode o app vinculando ao IP: use variável de ambiente `ASPNETCORE_URLS` antes de rodar:
```powershell
$env:ASPNETCORE_URLS = "http://0.0.0.0:5000"
dotnet run
```
- Então acesse de outra máquina `http://192.168.0.50:5000`


7) Serviço Windows (opcional)
- Para que o serviço suba automaticamente, use um utilitário como `nssm` para criar um serviço que execute `dotnet run` ou compile e execute o `dll` publicado.


8) Obter IDs do Asana
- Token: Asana → Minha conta → Apps → Personal access tokens
- Project ID: abra o projeto no Asana e copie o ID da URL (números no final) ou use API para listar projetos
- Tag ID: crie a tag no projeto e copie o ID (pode usar API para listar tags do workspace)


9) Uploads
- Arquivos enviados são salvos na pasta `/uploads` do projeto. Se quiser apagar periodicamente, crie um job para limpar.