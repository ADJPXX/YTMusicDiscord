# YouTube Music para Discord (Edge + C#)

Esta extensão lê a faixa atual do player do YouTube Music e envia título, artista e estado de reprodução para um host local em C#. O host usa o canal IPC local do Discord para atualizar o Rich Presence. O Discord para Windows precisa estar aberto.

## Preparar o Discord

1. Acesse [Discord Developer Portal](https://discord.com/developers/applications) e crie uma aplicação, por exemplo, com o nome **YouTube Music**.
2. Copie o **Application ID** em *General Information*. Esse identificador não é um token nem uma senha.
3. Se quiser, configure o nome e o ícone da aplicação que aparecerão na atividade.

O Discord exige uma aplicação registrada e um Application ID válido para publicar Rich Presence. O host já implementa o protocolo IPC e não usa bibliotecas externas.

## Instalar

1. No Edge, abra `edge://extensions` e ligue **Modo de desenvolvedor**.
2. Selecione **Carregar descompactada** e escolha a pasta `extension` deste pacote.
3. Copie o ID da extensão que aparece no cartão dela.
4. Abra PowerShell nesta pasta do pacote e execute:

   ```powershell
   .\install.ps1 -ExtensionId SEU_ID_DA_EXTENSAO -DiscordApplicationId SEU_APPLICATION_ID
   ```

   O instalador compila o host C# para Windows x64, grava a configuração e registra o host apenas para o seu usuário. É necessário ter o **.NET 10 SDK** instalado caso o executável ainda não exista em `native-host\publish`.

5. Volte a `edge://extensions`, recarregue a extensão e atualize a aba do YouTube Music.
6. Confirme no Discord que a opção de compartilhar atividades detectadas está habilitada.

Quando a faixa toca, o título e o artista aparecem na atividade do Discord. Ao pausar, a atividade marca a faixa como pausada; ao fechar a aba ou sair do YouTube Music, ela é limpa.

## Remover

Execute `.\uninstall.ps1` no PowerShell e remova a extensão em `edge://extensions`. O script de remoção apaga apenas o registro do host no perfil atual do Windows.

## Arquivos

- `extension/`: extensão Manifest V3 do Edge.
- `native-host/`: código-fonte C# do host Native Messaging e da conexão IPC com o Discord.
- `install.ps1` / `uninstall.ps1`: registro e remoção do host no Windows.

## Criar uma release do host pelo GitHub Actions

1. Envie os arquivos atualizados para o GitHub.
2. No repositório, abra **Actions** e selecione **Build Host Installer Release**.
3. Clique em **Run workflow** e informe uma versão nova, por exemplo `1.0.3` (sem `v`).
4. O workflow compila o host C# para Windows x64, cria `YTMusicDiscord-Host-Setup.exe` com Inno Setup e anexa o instalador a uma GitHub Release.

A extensão do Edge continua sendo enviada e atualizada pelo Partner Center. O instalador da Release contém apenas o host local e a configuração Native Messaging.

## Instalação da versão pública

Depois que a extensão for aprovada e estiver disponível na loja do Edge:

1. Instale a extensão pela Microsoft Edge Add-ons.
2. Baixe e execute o [instalador do host](https://github.com/ADJPXX/YTMusicDiscord/releases/latest/download/YTMusicDiscord-Host-Setup.exe).
3. Mantenha o aplicativo Discord para Windows aberto e reproduza uma faixa no YouTube Music.

O instalador do host é separado da extensão e não exige o .NET SDK no computador de quem instala.
