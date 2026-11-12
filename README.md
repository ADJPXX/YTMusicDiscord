# YouTube Music para Discord (Edge + C#)

Esta extensão envia título, artista e estado de reprodução do YouTube Music para a atividade do Discord. Ela usa um host local para Windows; mantenha o Discord para Windows aberto enquanto escuta música.

## Instalação

1. Instale a [extensão pela Microsoft Edge Add-ons](https://microsoftedge.microsoft.com/addons/detail/youtube-music-para-discor/bncpjpihhfpgngdbaiadlamggpkingmf).
2. Baixe e execute o [instalador do host para Windows x64](https://github.com/ADJPXX/YTMusicDiscord/releases/latest/download/YTMusicDiscord-Host-Setup.exe). Ele configura o host local para o usuário atual e não exige o .NET SDK.
3. Se o Edge estava aberto durante a instalação, feche e abra-o novamente. Atualize a aba do YouTube Music e reproduza uma faixa.
4. No Discord, habilite o compartilhamento de atividades detectadas.

O instalador já configura a aplicação Discord usada pelo projeto. Você não precisa criar uma aplicação no Discord Developer Portal para instalar a versão pública. A extensão é instalada e atualizada pela loja do Edge; a GitHub Release fornece somente o host local.

## Remover

Desinstale **YouTube Music para Discord - Host** em *Configurações do Windows > Aplicativos > Aplicativos instalados* e remova a extensão em `edge://extensions`.

## Desenvolvimento e instalação manual

Use estes passos apenas para testar uma cópia local da extensão. Para o uso normal, siga a instalação da loja acima.

1. No Edge, abra `edge://extensions`, habilite **Modo de desenvolvedor** e selecione **Carregar descompactada**. Escolha a pasta `extension` deste repositório.
2. Copie o ID exibido no cartão da extensão.
3. Crie uma aplicação no [Discord Developer Portal](https://discord.com/developers/applications) e copie o **Application ID** em *General Information*. Esse identificador não é um token nem uma senha.
4. Abra PowerShell na pasta do repositório e execute:

   ```powershell
   .\install.ps1 -ExtensionId SEU_ID_DA_EXTENSAO -DiscordApplicationId SEU_APPLICATION_ID
   ```

   O script compila o host C# para Windows x64 se `native-host\publish` ainda não contiver o executável. Nesse caso, instale o **.NET 10 SDK**.
5. Recarregue a extensão em `edge://extensions`, atualize a aba do YouTube Music e confirme que o compartilhamento de atividades detectadas está habilitado no Discord.

Para remover o host instalado manualmente, execute `.\uninstall.ps1` no PowerShell e remova a extensão em `edge://extensions`.

## Arquivos

- `extension/`: extensão Manifest V3 do Edge.
- `native-host/`: código-fonte C# do host Native Messaging e da conexão IPC com o Discord.
- `install.ps1` / `uninstall.ps1`: instalação e remoção manuais do host no Windows.
- `installer/`: configuração do instalador do host publicado nas GitHub Releases.

## Criar uma release do host pelo GitHub Actions

1. Envie as alterações para o GitHub.
2. No repositório, abra **Actions** e selecione **Build Host Installer Release**.
3. Clique em **Run workflow** e informe uma versão nova, por exemplo `1.0.4` (sem `v`).
4. O workflow compila o host C# para Windows x64, cria `YTMusicDiscord-Host-Setup.exe` com Inno Setup e anexa o instalador a uma GitHub Release.

A extensão do Edge continua sendo enviada e atualizada pelo Partner Center. Atualizações da extensão devem ser publicadas pela loja; a GitHub Release é para distribuir o host local.
