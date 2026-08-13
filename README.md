# Alpakasoelde 🦙

The homepage of our alpaca farm.  
[Take a look](https://alpakasoelde.at)

## Color codes to use

| Hex     | Name        |
| ------- | ----------- |
| #9abfba | Auwasser    |
| #d7854b | Backstein   |
| #e1b14a | Blütenhonig |
| #8da5d3 | Himmelblau  |
| #b9bc49 | Jungtrieb   |
| #fbf7ed | Schurwolle  |
| #1f1f1d | Schwarz     |
| #4b5b73 | Taubenblau  |
| #698a6e | Weidegrün   |
| #ffffff | Weiss       |

## Infrastructure

The Azure estate is managed with Bicep under `infrastructure/` (`main.bicep` is
resource-group scoped, `main-subscription.bicep` covers the cost budget). Changes
to `infrastructure/**` deploy automatically via the `infra-deploy.yml` workflow,
which previews with `what-if` on PRs and applies on `main`. See `plan.md` for the
adoption plan and `AGENTS.md` for the exact `az` commands.
