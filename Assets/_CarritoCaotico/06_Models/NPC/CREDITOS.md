# Créditos de los personajes de los compradores rivales

Modelos y animaciones **CC0 1.0** (dominio público) de **Kay Lousberg** ([kaylousberg.com](https://www.kaylousberg.com)), descargados de sus repositorios oficiales en GitHub. La licencia original de cada pack está junto a sus archivos.

| Carpeta | Pack original | Personajes | Supermercado |
|---|---|---|---|
| `Aventureros/` | [KayKit – Character Pack: Adventurers 1.0](https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0) | Knight, Barbarian, Mage, Rogue, RogueHooded | Súper Caótico |
| `Esqueletos/` | [KayKit – Character Pack: Skeletons 1.0](https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Skeletons-1.0) | Skeleton_Minion, Skeleton_Warrior, Skeleton_Rogue, Skeleton_Mage | Súper Embrujado |

Todos comparten el mismo esqueleto (`Rig`, 41 huesos), así que comparten también las animaciones.

## Cambios respecto a los originales

Los FBX originales pesan unos 20 MB cada uno porque traen 75 animaciones. Se recortaron con Blender (`Tools/recortar_kaykit.py`):

- Cada personaje: sin armas ni escudos (objetos colgados de `handslot.*`) y sin animaciones. Malla, esqueleto, ejes y escala son idénticos al original.
- `NPC_Animaciones.fbx`: solo el esqueleto con `Idle`, `Walking_A`, `PickUp` y `Cheer`, sacadas de `Knight.fbx`.

Así los 9 personajes y las animaciones ocupan unos 5,6 MB en lugar de 180 MB.
