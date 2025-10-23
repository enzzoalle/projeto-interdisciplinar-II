# 🎥 Lance Certo

O **Lance Certo** é uma aplicação de desktop para **Windows**, desenvolvida em **C# com WPF**, que permite capturar os últimos **30 segundos de vídeo da sua webcam** com o clique de um botão.  
O projeto conta com um sistema de autenticação de usuários, onde cada usuário pode gerenciar sua própria lista de replays salvos.

---

## ✨ Features

- **Captura em Tempo Real:** Visualização ao vivo da câmera do dispositivo.
- **Replay Instantâneo:** Salva os últimos 30 segundos de vídeo que estão em um buffer de memória.
- **Autenticação de Usuário:** Sistema completo de registro e login de usuários.
- **Replays Personalizados:** Cada usuário tem acesso apenas à sua própria galeria de replays.
- **Gerenciamento de Clipes:**
    - **Renomear:** Altere o nome de um replay salvo através de uma janela de diálogo.
    - **Download:** Salve uma cópia do arquivo de vídeo (`.mp4`) na pasta de Downloads do usuário.
    - **Excluir:** Remova o replay do banco de dados e o arquivo de vídeo do disco de forma permanente (com confirmação).
- **Arquitetura Limpa:** Construído seguindo o padrão **MVVM (Model-View-ViewModel)** em uma estrutura de **Janela Única (Single-Window Application)**.
- **Interface Moderna:** Tema escuro e layout limpo para uma experiência de usuário agradável.
- **Configuração Segura:** Utiliza o **Secret Manager** do .NET para proteger dados sensíveis, como a string de conexão do banco de dados.

---

## 🛠️ Tecnologias Utilizadas

- **Linguagem:** C#
- **Framework:** .NET 8
- **Interface Gráfica:** WPF (Windows Presentation Foundation)
- **Banco de Dados:** PostgreSQL
- **Bibliotecas Principais:**
    - [OpenCvSharp](https://github.com/shimat/opencvsharp): Para captura e processamento de vídeo.
    - [Npgsql](https://www.npgsql.org/): Conector oficial para PostgreSQL no .NET.
    - [Microsoft.Extensions.Configuration](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration): Para gerenciamento de configurações e segredos.

---

## 🚀 Como Rodar o Projeto Localmente

Para clonar e executar esta aplicação em sua máquina, siga os passos abaixo.

### ⚙️ Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Uma IDE .NET (JetBrains Rider ou Visual Studio)
- [PostgreSQL](https://www.postgresql.org/download/) instalado e rodando
- Uma webcam conectada e com drivers instalados
- [Git](https://git-scm.com/downloads)

---

### 1️⃣ Clonar o Repositório

Abra um terminal e clone o projeto:

```bash
git clone https://github.com/enzzoalle/projeto-interdisciplinar-II.git
```

### 2️⃣ Configuração do Banco de Dados

Você precisa criar o banco de dados e as tabelas que a aplicação espera encontrar.  Abra sua ferramenta de gerenciamento do PostgreSQL (DBeaver, pgAdmin, etc.)
e execute os seguintes scripts SQL:
```sql
-- 1. Crie o banco de dados
CREATE DATABASE dev_replays;

-- 2. Conecte-se ao banco 'dev_replays' pelo DBeaver e execute as criações de tabela abaixo

-- Tabela de Usuários
CREATE TABLE public.users (
    id serial4 NOT NULL,
    nome varchar(22) NOT NULL,
    senha varchar(22) NOT NULL,
    CONSTRAINT users_nome_key UNIQUE (nome),
    CONSTRAINT users_pkey PRIMARY KEY (id)
);

-- Tabela de Replays
CREATE TABLE public.replays (
    id serial4 NOT NULL,
    data_gravacao timestamp NOT NULL,
    caminho_arquivo text NOT NULL,
    duracao_segundos int4 NOT NULL,
    user_id int4 NOT NULL,
    nome varchar(44) NULL,
    caminho_thumbnail text NULL,
    CONSTRAINT replays_pkey PRIMARY KEY (id)
);

-- chaves estrangeiras
ALTER TABLE public.replays ADD CONSTRAINT fk_user FOREIGN KEY (user_id) REFERENCES public.users(id) ON DELETE CASCADE;
```

### 3️⃣ Configuração do User Secrets

Este projeto protege a string de conexão do banco de dados usando o Secret Manager do .NET.
Ela não está no appsettings.json.

Abra um terminal na pasta raiz do projeto (onde está o arquivo .csproj) e execute:
```
dotnet user-secrets init
```

Agora adicione a sua string de conexão aos segredos.
Substitua os placeholders seu_usuario_pg e sua_senha_pg pelas suas credenciais do PostgreSQL:
```
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Username=seu_usuario_pg;Password=sua_senha_pg;Database=dev_replays"
```
