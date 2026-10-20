# LimpaRAM

Programa para Windows que libera memória RAM com um único botão. Abre, aperta e pronto.

**[⬇ Baixar a última versão](../../releases/latest/download/LimpaRAM.exe)**

## O que ele faz

- Mostra a RAM em uso em tempo real
- Ao apertar **LIMPAR RAM**:
  - libera a memória que cada processo está segurando sem usar
  - limpa o cache de arquivos do sistema
  - grava a lista modificada no disco
  - esvazia a lista standby
- Mostra quanto foi liberado

Precisa rodar como administrador (o próprio programa pede).

## Como o .exe é gerado

Toda vez que o código em `src/` muda, o GitHub Actions compila o `LimpaRAM.exe` numa máquina Windows e publica uma nova versão em **Releases**. O link de download acima sempre aponta para a mais recente.

Para compilar no seu próprio PC: entre em `src/` e dê dois cliques em `COMPILAR.bat`.

## Estrutura

```
src/                    código do programa
  LimpaRAM.cs
  app.manifest          pede permissão de administrador
  COMPILAR.bat          compila localmente
docs/index.html         página do GitHub Pages
.github/workflows/      compilação automática
```

## Aviso

Liberar RAM não deixa o PC mais rápido por si só: o Windows usa a memória sobrando como cache de propósito. Ajuda em casos específicos, como travadas em jogos por standby cheio ou programas que vazam memória.
