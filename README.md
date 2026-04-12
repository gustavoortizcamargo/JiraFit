# JiraFit - Nutrição de Bolso com Inteligência Artificial

O **JiraFit** é um assistente pessoal de nutrição e bem-estar projetado para atuar silenciosamente através do WhatsApp. Envie fotos, áudios ou textos descritivos da sua refeição, e a Inteligência Artificial calcula os macros, ajusta os valores nutricionais à sua meta basal, avalia seu progresso diário e envia feedbacks motivacionais.

## Funcionalidades Principais

* **Comunicação Direta via WhatsApp (Twilio API)**: Sem aplicativos pesados, tudo no hub onde o usuário já se encontra.
* **Inteligência Artificial (Gemini 1.5 Flash)**: Extração rápida de macronutrientes (Calorias, Proteínas, Carboidratos, Gorduras) a partir de descrições multimodais (Texto ou Imagem do prato).
* **Onboarding Inteligente**: Cálculo automático de Taxa Metabólica Basal (TMB) e Valor Energético Total (VET) guiado pelo objetivo (Deficit de Calorias, Ganho Muscular, ou Manutenção).
* **Modo "Paladar Infantil"**: Opção ajustável nas configurações que instrui a IA a fazer sugestões de reeducação baseadas em combinações com texturas mais amigáveis e substituições palatáveis.

---

## 🛠 Arquitetura e Stack

Este projeto implementa uma abordagem em **Clean Architecture** e princípios de Domain-Driven Design (DDD) garantindo escalabilidade, desacoplamento e um sistema coeso capaz de sustentar crescimento e novas interfaces (ex., o futuro Dashboard em Angular).

* **Plataforma**: .NET 8 (C#)
* **Banco de Dados**: PostgreSQL (EF Core - Code First)
* **Design de Software**:
  * Clean Architecture (Domain, Application, Infrastructure e API)
  * Result Pattern (Redução de Throws e Exceptions para Controle Estrutural Contínuo)
  * Dependency Injection System e Middlewares Dedicados
* **Concorrência e Performance**: Delegação via `.NET Channels` (`System.Threading.Channels`) em conjunto com BackgroundServices para libertar chamadas webhook do Twilio (`202 Accepted`) minimizando custos computacionais.

---

## 🚀 Como Rodar e Testar

### Pré-Requisitos
- SDK do [.NET 8](https://dotnet.microsoft.com/download/dotnet/8.0).
- Um servidor **PostgreSQL** rodando. 

### 1. Migrations e Database
Configurando a `ConnectionString`: Se estiver localmente, declare no seu `appsettings.json` ou como variável de ambiente `DATABASE_URL`.
```bash
# Na pasta da API do projeto, inicie as migrations
cd JiraFit.API
dotnet ef migrations add InitialMigration --project ../JiraFit.Infrastructure
dotnet ef database update
```

### 2. Rodando o Projeto
```bash
dotnet run --project JiraFit.API
```
*Observação: A API usará as variáveis de ambiente `$PORT` (quando em Produção/Railway) ou localmente em 8080 caso não estipulado.*

---
