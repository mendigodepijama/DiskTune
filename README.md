# 💽 DiskTune - Disk Health, S.M.A.R.T. & Surface Scanner (.NET 8 WPF)

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)]()
[![WPF](https://img.shields.io/badge/UI-WPF%20XAML-purple.svg)]()
[![Tests](https://img.shields.io/badge/Tests-xUnit%20(15%2F15%20Passing)-brightgreen.svg)]()
[![License](https://img.shields.io/badge/License-MIT-green.svg)]()

**DiskTune** é uma ferramenta desktop moderna e de alto desempenho para Windows desenvolvida em **C# .NET 8 (WPF)**, inspirada no clássico HD Tune, focada no diagnóstico físico completo, monitoramento de saúde **S.M.A.R.T.**, especificações técnicas detalhadas de hardware e escaneamento visual de superfície de unidades de armazenamento (HDDs e SSDs NVMe/SATA).

---

## 🚀 Principais Módulos e Funcionalidades

### 1. 🛡️ Aba: Verificação de Superfície (Error Scan)
* **Varredura Física de Baixo Nível (Raw Disk I/O)**:
  - Comunicação direta com o dispositivo físico (`\\.\PhysicalDriveX`) via chamadas nativas Win32 (`CreateFileW`, `ReadFile`).
  - Flag `FILE_FLAG_NO_BUFFERING` para desativar o cache de RAM do Windows e ler o prato magnético / células de memória reais.
  - Alinhamento a múltiplos de setor de 4096 bytes (Advanced Format e NVMe).
  - Cancelamento e timeout de E/S inteligente (`CancelIoEx`) para evitar congelamento do app ao bater em bad blocks físicos.
* **Matriz Visual de Alta Performance**:
  - Milhares de blocos desenhados diretamente na GPU via `WriteableBitmap` (`BlockCanvas`).
  - Throttle de UI a 30 FPS (~33ms) para manter a janela leve e responsiva.
  - 5 níveis visuais de saúde:
    - 🟢 **Verde Vivo (< 50ms)**: Excelente
    - 🟢 **Verde Normal (50ms - 150ms)**: Bom
    - 🟡 **Amarelo (150ms - 500ms)**: Lento / Setor instável
    - 🟠 **Laranja (500ms - 2000ms)**: Crítico
    - 🔴 **Vermelho (> 2000ms ou Timeout)**: Bad Block / Danificado

### 2. ❤️ Aba: Saúde & S.M.A.R.T. (Health)
* **Banner de Integridade Geral**:
  - Classificação instantânea: `SAUDÁVEL`, `ATENÇÃO` ou `CRÍTICO`.
  - Diagnóstico em linguagem clara explicando exatamente o estado do disco.
* **Métricas Rápidas de Destaque**:
  - 🌡️ **Temperatura Atual do Disco**: Monitoramento térmico em tempo real.
  - ⏱️ **Horas Ligado / Tempo de Uso**: Total de horas de funcionamento e conversão aproximada para dias e anos (ex: `44.256 h (~5,0 anos)`).
  - ⚡ **Ciclos de Inicialização**: Quantidade total de vezes em que o disco foi ligado/desligado.
  - ⚠️ **Setores Realocados (ID 05)**: Quantidade de setores defeituosos já substituídos pela área de reserva.
  - 🔌 **Erros de Cabo/Interface CRC (ID C7)**: Histórico de falhas de transmissão na interface SATA, ideal para diagnosticar cabos frouxos ou defeituosos.
* **Tabela Completa de Atributos**:
  - ID em Hexadecimal e Decimal, Nome legível, Valor Atual, Pior Valor Registrado, Limiar (Threshold), Valor Bruto Interpretado e Badge colorido de status.

### 3. ℹ️ Aba: Informações do Disco (Specs & Volumes)
* **Identificação Completa**: Modelo, Fabricante, Número de Série, Revisão de Firmware, Identificador Físico do Windows (`DeviceID`) e ID PNP.
* **Interface & Tecnologia**: Tipo de Barramento (SATA, NVMe, USB), Tipo de Mídia (SSD / HDD Mecânico), Rotação (RPM) e Status do Sistema.
* **Capacidade & Geometria**: Capacidade formatada e em Bytes exatos, Total de Setores LBA, Tamanho do Setor (Bytes por Setor), Cilindros, Cabeças e Trilhas.
* **Partições e Volumes**: Lista com cards de todas as partições do disco, exibindo letra, rótulo, sistema de arquivos (NTFS/FAT), espaço livre/usado e barra gráfica de ocupação.

### 4. 🔄 Detecção Dinâmica de Discos
* **Botão "Recarregar"**: Atualiza a lista de dispositivos físicos imediatamente, permitindo conectar ou desconectar HDDs/SSDs/unidades USB sem fechar o programa.

---

## 🛠️ Tecnologias Utilizadas

* **Linguagem & Framework**: C# 12 / .NET 8.0 (Windows Desktop)
* **Interface Gráfica**: WPF (Windows Presentation Foundation) com Tema Dark moderno e responsivo
* **APIs Nativas e WMI**: `kernel32.dll` (Overlapped I/O, SafeFileHandle), WMI `root\cimv2` (`Win32_DiskDrive`, `Win32_LogicalDisk`), WMI `root\wmi` (`MSStorageDriver_FailurePredictData`, `MSStorageDriver_FailurePredictThresholds`) e `root\Microsoft\Windows\Storage` (`MSFT_PhysicalDisk`)
* **Testes Automatizados**: xUnit e FluentAssertions (15 testes unitários)

---

## 🧪 Testes Automatizados

Para executar toda a suite de testes:

```bash
dotnet test DiskTune.sln
```

```text
Aprovado! – Com falha: 0, Aprovado: 15, Ignorado: 0, Total: 15, Duração: 27 ms
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
Execute o arquivo `run.bat` ou pelo terminal (com elevação de Administrador para leitura direta de discos físicos):

```bash
dotnet run --project DiskTune.csproj
```

---

## 📄 Licença

Distribuído sob a licença MIT.
