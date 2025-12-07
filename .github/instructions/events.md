# Fellowship Combat Log Event Schema (C# .NET 10)

This document defines the event types and properties found in the Fellowship combat log JSON export. It is intended to guide the generation of C# models for deserialization.

## JSON Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "Fellowship Combat Log Events",
  "description": "Schema for Fellowship game combat log events export.",
  "type": "object",
  "properties": {
    "data": {
      "type": "object",
      "properties": {
        "reportData": {
          "type": "object",
          "properties": {
            "report": {
              "type": "object",
              "properties": {
                "events": {
                  "type": "object",
                  "properties": {
                    "data": {
                      "type": "array",
                      "items": {
                        "$ref": "#/definitions/CombatEvent"
                      }
                    }
                  },
                  "required": [
                    "data"
                  ]
                }
              },
              "required": [
                "events"
              ]
            }
          },
          "required": [
            "report"
          ]
        }
      },
      "required": [
        "reportData"
      ]
    }
  },
  "required": [
    "data"
  ],
  "definitions": {
    "CombatEvent": {
      "type": "object",
      "properties": {
        "timestamp": { "type": "integer" },
        "type": { "type": "string" },
        "sourceID": { "type": "integer" },
        "targetID": { "type": "integer" },
        "abilityGameID": { "type": "integer" },
        "fight": { "type": "integer" }
      },
      "required": [
        "timestamp",
        "type",
        "sourceID",
        "targetID",
        "fight"
      ],
      "allOf": [
        {
          "if": { "properties": { "type": { "const": "combatantinfo" } } },
          "then": { "$ref": "#/definitions/EventCombatantInfo" }
        },
        {
          "if": { "properties": { "type": { "const": "damage" } } },
          "then": { "$ref": "#/definitions/EventDamage" }
        },
        {
          "if": { "properties": { "type": { "const": "heal" } } },
          "then": { "$ref": "#/definitions/EventHeal" }
        },
        {
          "if": { "properties": { "type": { "const": "cast" } } },
          "then": { "$ref": "#/definitions/EventCast" }
        },
        {
          "if": { "properties": { "type": { "enum": ["applybuff", "applydebuff", "refreshbuff", "refreshdebuff", "removebuff", "removedebuff"] } } },
          "then": { "$ref": "#/definitions/EventAura" }
        },
        {
          "if": { "properties": { "type": { "enum": ["applybuffstack", "removebuffstack"] } } },
          "then": { "$ref": "#/definitions/EventStack" }
        },
        {
          "if": { "properties": { "type": { "const": "absorbed" } } },
          "then": { "$ref": "#/definitions/EventAbsorbed" }
        },
        {
          "if": { "properties": { "type": { "const": "death" } } },
          "then": { "$ref": "#/definitions/EventDeath" }
        }
      ]
    },
    "EventCombatantInfo": {
      "properties": {
        "faction": { "type": "integer" },
        "specID": { "type": "integer" },
        "expansion": { "type": "integer" },
        "itemLevel": { "type": "integer" },
        "strength": { "type": "integer" },
        "agility": { "type": "integer" },
        "stamina": { "type": "integer" },
        "intellect": { "type": "integer" },
        "crit": { "type": "integer" },
        "haste": { "type": "integer" },
        "mastery": { "type": "integer" },
        "versatility": { "type": "integer" },
        "armor": { "type": "integer" },
        "dodge": { "type": "integer" },
        "parry": { "type": "integer" },
        "block": { "type": "integer" },
        "gear": {
          "type": "array",
          "items": { "$ref": "#/definitions/GearItem" }
        },
        "auras": { "type": "array" },
        "talents": { "type": "array" },
        "weaponTraits": { "type": "array" }
      }
    },
    "GearItem": {
      "type": "object",
      "properties": {
        "id": { "type": "integer" },
        "quality": { "type": "integer" },
        "icon": { "type": "string" },
        "name": { "type": "string" },
        "itemLevel": { "type": "integer" },
        "upgrades": { "type": "integer" },
        "maxUpgrades": { "type": "integer" },
        "hasGemSocket": { "type": "boolean" },
        "gem": {
          "type": ["object", "null"],
          "properties": {
            "id": { "type": "integer" },
            "icon": { "type": "string" },
            "name": { "type": "string" },
            "quality": { "type": "integer" }
          }
        },
        "attributes": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "id": { "type": "integer" },
              "name": { "type": "string" },
              "value": { "type": "integer" }
            }
          }
        }
      }
    },
    "EventDamage": {
      "properties": {
        "hitType": { "type": "integer" },
        "amount": { "type": "integer" },
        "unmitigatedAmount": { "type": "integer" },
        "overkill": { "type": "integer" },
        "mitigated": { "type": "integer" },
        "absorbed": { "type": "integer" },
        "tick": { "type": "boolean" },
        "sourceMarker": { "type": "integer" },
        "targetMarker": { "type": "integer" }
      },
      "required": ["amount"]
    },
    "EventHeal": {
      "properties": {
        "hitType": { "type": "integer" },
        "amount": { "type": "integer" },
        "overheal": { "type": "integer" },
        "absorbed": { "type": "integer" },
        "tick": { "type": "boolean" },
        "sourceMarker": { "type": "integer" },
        "targetMarker": { "type": "integer" }
      },
      "required": ["amount"]
    },
    "EventCast": {
      "properties": {
        "activation": { "type": "boolean" },
        "targetInstance": { "type": "integer" }
      }
    },
    "EventAura": {
      "properties": {
        "duration": { "type": "integer" },
        "extraAbilityGameID": { "type": "integer" },
        "absorb": { "type": "integer" }
      }
    },
    "EventStack": {
      "properties": {
        "stack": { "type": "integer" }
      },
      "required": ["stack"]
    },
    "EventAbsorbed": {
      "properties": {
        "attackerID": { "type": "integer" },
        "amount": { "type": "integer" },
        "extraAbilityGameID": { "type": "integer" }
      },
      "required": ["amount", "attackerID"]
    },
    "EventDeath": {
      "properties": {
        "killScore": { "type": "integer" },
        "targetInstance": { "type": "integer" }
      }
    }
  }
}
```


## Base Event
All events inherit from a common base structure.

```csharp
public abstract class CombatLogEvent
{
    /// <summary>
    /// Time of the event in milliseconds from the start of the log/fight.
    /// </summary>
    public long Timestamp { get; set; }

    /// <summary>
    /// The discriminator for the event type (e.g., "damage", "cast").
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// ID of the entity initiating the event.
    /// </summary>
    public int SourceID { get; set; }

    /// <summary>
    /// ID of the target entity.
    /// </summary>
    public int TargetID { get; set; }

    /// <summary>
    /// ID of the ability or spell associated with the event.
    /// </summary>
    public int AbilityGameID { get; set; }

    /// <summary>
    /// ID of the specific fight/encounter this event belongs to.
    /// </summary>
    public int Fight { get; set; }
}
````

-----

## Event Definitions

### 1\. Combatant Info (`combatantinfo`)

Emitted at the start of a fight, detailing player stats, gear, and loadout.

```csharp
public class CombatantInfoEvent : CombatLogEvent
{
    // Faction and Spec
    public int Faction { get; set; }
    public int SpecID { get; set; }
    public int Expansion { get; set; }

    // Primary Stats
    public int Strength { get; set; }
    public int Agility { get; set; }
    public int Stamina { get; set; }
    public int Intellect { get; set; }

    // Secondary Stats
    public int Crit { get; set; }
    public int Haste { get; set; }
    public int Mastery { get; set; }
    public int Versatility { get; set; }

    // Defensive Stats
    public int Armor { get; set; }
    public int Dodge { get; set; }
    public int Parry { get; set; }
    public int Block { get; set; }

    // Gear & Loadout
    public int ItemLevel { get; set; }
    public List<GearItem> Gear { get; set; } = [];
    public List<AuraInfo> Auras { get; set; } = [];
    public List<TalentInfo> Talents { get; set; } = [];
    public List<TraitInfo> WeaponTraits { get; set; } = [];
}

public class GearItem
{
    public int Id { get; set; }
    public int Quality { get; set; }
    public string Icon { get; set; }
    public string Name { get; set; }
    public int ItemLevel { get; set; }
    public int Upgrades { get; set; }
    public int MaxUpgrades { get; set; }
    public bool HasGemSocket { get; set; }
    public GemInfo? Gem { get; set; }
    public List<ItemAttribute> Attributes { get; set; } = [];
}

public class GemInfo
{
    public int Id { get; set; }
    public string Icon { get; set; }
    public string Name { get; set; }
    public int Quality { get; set; }
}

public class ItemAttribute
{
    public int Id { get; set; }
    public string Name { get; set; } // e.g., "Stamina", "Haste"
    public int Value { get; set; }
}
```

### 2\. Damage (`damage`)

Records damage dealt to a unit.

```csharp
public class DamageEvent : CombatLogEvent
{
    /// <summary>
    /// 1 = Normal, 2 = Crit.
    /// </summary>
    public int HitType { get; set; }

    /// <summary>
    /// Actual damage subtracted from target HP.
    /// </summary>
    public int Amount { get; set; }

    /// <summary>
    /// Raw damage calculated before mitigation (armor/resists).
    /// </summary>
    public int UnmitigatedAmount { get; set; }

    /// <summary>
    /// Damage dealt exceeding the target's remaining health.
    /// </summary>
    public int Overkill { get; set; }

    /// <summary>
    /// Damage prevented by mitigation (Armor/Resist).
    /// </summary>
    public int Mitigated { get; set; }

    /// <summary>
    /// Damage absorbed by shields (e.g., Power Word: Shield).
    /// </summary>
    public int Absorbed { get; set; }

    /// <summary>
    /// True if this is a DoT (Damage over Time) tick.
    /// </summary>
    public bool Tick { get; set; }

    // Raid Markers (Optional)
    public int? SourceMarker { get; set; }
    public int? TargetMarker { get; set; }
}
```

### 3\. Healing (`heal`)

Records healing applied to a unit.

```csharp
public class HealEvent : CombatLogEvent
{
    /// <summary>
    /// 1 = Normal, 2 = Crit.
    /// </summary>
    public int HitType { get; set; }

    /// <summary>
    /// Effective healing applied.
    /// </summary>
    public int Amount { get; set; }

    /// <summary>
    /// Healing done exceeding the target's max HP.
    /// </summary>
    public int Overheal { get; set; }

    /// <summary>
    /// True if this is a HoT (Heal over Time) tick.
    /// </summary>
    public bool Tick { get; set; }

    /// <summary>
    /// Damage prevented/absorbed (specific to shield-heals).
    /// </summary>
    public int Absorbed { get; set; }

    public int? SourceMarker { get; set; }
    public int? TargetMarker { get; set; }
}
```

### 4\. Casts (`cast`, `begincast`)

`begincast` denotes the start of a cast bar. `cast` denotes the successful completion or instant activation.

```csharp
public class CastEvent : CombatLogEvent
{
    /// <summary>
    /// True if the spell successfully activated (for 'cast' events).
    /// </summary>
    public bool Activation { get; set; }
    
    /// <summary>
    /// Identifier for specific unit instance if multiple exist.
    /// </summary>
    public int TargetInstance { get; set; }
}
```

### 5\. Buffs & Debuffs (Auras)

**Types:** `applybuff`, `applydebuff`, `refreshbuff`, `refreshdebuff`, `removebuff`, `removedebuff`

```csharp
public class AuraEvent : CombatLogEvent
{
    /// <summary>
    /// ID of the ability associated with the aura application/removal.
    /// Occasionally distinct from AbilityGameID if a proxy spell caused it.
    /// </summary>
    public int ExtraAbilityGameID { get; set; }

    /// <summary>
    /// Duration of the aura in milliseconds (for apply/refresh).
    /// </summary>
    public int Duration { get; set; }

    /// <summary>
    /// For shield auras: The remaining absorb amount (on remove) or max absorb (on apply).
    /// </summary>
    public int Absorb { get; set; }
}
```

### 6\. Stacks (`applybuffstack`, `removebuffstack`)

Records changes in stack counts for stacking auras.

```csharp
public class AuraStackEvent : CombatLogEvent
{
    /// <summary>
    /// The new stack count after the event.
    /// </summary>
    public int Stack { get; set; }
}
```

### 7\. Absorb Execution (`absorbed`)

Triggered when a shield actively prevents damage.

```csharp
public class AbsorbedEvent : CombatLogEvent
{
    /// <summary>
    /// The entity that dealt the damage being absorbed.
    /// </summary>
    public int AttackerID { get; set; }

    /// <summary>
    /// Amount of damage prevented.
    /// </summary>
    public int Amount { get; set; }

    /// <summary>
    /// The spell ID of the shield doing the absorbing.
    /// </summary>
    public int ExtraAbilityGameID { get; set; }
}
```

### 8\. Death (`death`)

Records the death of a unit.

```csharp
public class DeathEvent : CombatLogEvent
{
    /// <summary>
    /// Logic score for the kill.
    /// </summary>
    public int KillScore { get; set; }

    public int TargetInstance { get; set; }
}
```