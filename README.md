# 💽 DiskTune - Disk Health & Surface Scanner (.NET 8 WPF)

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)]()
[![WPF](https://img.shields.io/badge/UI-WPF%20XAML-purple.svg)]()
[![Tests](https://img.shields.io/badge/Tests-xUnit%20(9%2F9%20Passing)-brightgreen.svg)]()
[![License](https://img.shields.io/badge/License-MIT-green.svg)]()

**DiskTune** é uma ferramenta desktop de alto desempenho para Windows desenvolvida em **C# .NET 8 (WPF)**, focada no diagnóstico físico, medição de latência e escaneamento visual da superfície de unidades de armazenamento (HDDs e SSDs NVMe/SATA).

---

## 🚀 Principais Funcionalidades

* **Varredura Física de Baixo Nível (Raw Disk I/O)**:
  - Comunicação direta com dispositivos físicos (`\\.\PhysicalDriveX`) via chamadas nativas Win32 (`kernel32.dll` - `CreateFileW`, `ReadFile`).
  - Uso de `FILE_FLAG_NO_BUFFERING` para desativar o cache do Windows e medir a velocidade real de resposta dos setores do disco.
  - Alinhamento estrito a setores físicos de **4096 bytes** (Advanced Format / NVMe).

* **Renderização Visual em Alta Performance**:
  - Matriz visual de blocos desenhada diretamente na memória via `WriteableBitmap` (`BlockCanvas`).
  - **Throttle de UI a 30 FPS (33ms)** com enfileiramento concorrente em lote, garantindo que a interface gráfica permaneça fluida a 60 FPS mesmo sob transferências intensas de dados.
  - Diferenciação visual por cores conforme a latência do bloco:
    - 🟩 **Verde (< 50ms)**: Excelente
    - 🟢 **Verde Normal (50ms - 150ms)**: Bom
    - 🟨 **Amarelo (150ms - 500ms)**: Lento
    - 🟧 **Laranja (500ms - 2000ms)**: Crítico
    - 🟥 **Vermelho (> 2000ms ou Timeout)**: Setor Danificado / Bad Block

* **Confiabilidade e Segurança de Memória**:
  - Manipulação segura de handles Win32 com `SafeFileHandle` e blocos `try / finally` invioláveis.
  - Desinscrição explícita de eventos WPF no fechamento da janela, eliminando qualquer risco de memory leaks.
  - Formatação com `CultureInfo.InvariantCulture` para consistência universal de dados.

* **Modos de Escaneamento**:
  - **Quick Scan**: Blocos de 64 KB com intervalos espaçados para um diagnóstico rápido da unidade.
  - **Normal / Full Scan**: Blocos de 256 KB com inspeção detalhada de setores.

---

## 🛠️ Tecnologias Utilizadas

* **Linguagem & Framework**: C# 12 / .NET 8.0 (Windows Desktop)
* **Interface**: WPF (Windows Presentation Foundation) com XAML e aceleração gráfica DirectX
* **APIs Nativas (P/Invoke)**: `kernel32.dll` (Overlapped I/O, Event Handles, CancelIoEx)
* **Testes Automatizados**: xUnit, FluentAssertions e Moq

---

## 🧪 Suite de Testes Unitários

O projeto conta com uma suite de testes automatizados cobrindo modelos, formatação e ciclo de vida do scanner:

```bash
dotnet test DiskTune.sln
```

```text
Aprovado! – Com falha: 0, Aprovado: 9, Ignorado: 0, Total: 9, Duração: 13 ms
```

---

## 📦 Como Compilar e Executar

### Pré-requisitos
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
* Windows 10 ou 11 (64 bits)

### Compilando a Solução
```bash
dotnet build DiskTune.sln
```

### Executando a Aplicação
> **Nota de Privilégios**: Para realizar leitura física direta de discos brutos (`\\.\PhysicalDrive`), é necessário executar a aplicação como **Administrador** (o arquivo `app.manifest` já inclui a solicitação padrão `requireAdministrator`).

```bash
dotnet run --project DiskTune.csproj
```

---

## 📄 Licença

Distribuído sob a licença MIT.
