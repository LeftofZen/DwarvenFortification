# DwarvenFortification

A Dwarf Fortress–inspired colony simulation built in C# with MonoGame. Agents plan and execute multi-step tasks autonomously using a Goal-Oriented Action Planning (GOAP) system backed by a modular, data-driven item and building model.

## The core insight: this game is a graph search problem

Every mechanic in this game reduces to a single idea: **agents search for paths through a graph of world states**.

- **Nodes** are world states — each node is a set of active fact strings (`rest.ok`, `has.item-tag.mining`, `workstation.needs-inputs`, etc.).
- **Edges** are actions — each action transitions the world from one state to another by adding and removing facts. Actions have costs, preconditions, and body-part/organ/system requirements.
- **Goals** are target nodes — desired (or forbidden) fact sets that an agent is trying to reach.
- **The GOAP planner** is a backward-chaining search (analogous to A\*) that finds the minimum-cost path from the agent's current fact set to a goal's desired fact set.
- **Agents** are independent pathfinders traversing this graph simultaneously, competing for shared world resources.

This framing means the entire design space — adding items, buildings, actions, recipes, goals — is the same problem at every level: adding nodes and edges to the graph.

## Config as graph definition

The `Content/config/` directory is the **complete specification of the state-space graph**. No gameplay logic lives in the config; it only describes the graph topology.

| File             | Graph role              | What it defines                                                                                    |
| ---------------- | ----------------------- | -------------------------------------------------------------------------------------------------- |
| `facts.json`     | **Node vocabulary**     | All named fact strings (predicates) that can appear in a world state                               |
| `actions.json`   | **Edges**               | Transitions: preconditions, costs, effects (fact additions/removals), body requirements            |
| `goals.json`     | **Target nodes**        | Priority-ordered goals with desired/forbidden/required/blocked fact sets                           |
| `items.json`     | **World entities**      | Items defined by modular `tags` + `properties`; each item emits facts when in an agent's inventory |
| `objects.json`   | **World entities**      | Buildings and structures with build costs, storage rules, workstation recipes                      |
| `resources.json` | **World entities**      | Resource nodes (veins, trees) with required tool tags and yield items                              |
| `agents.json`    | **Starting conditions** | Agent archetypes with biological stats, inventory capacity, and starting items                     |

### Modular items

Items are not referenced by exact ID in action requirements or recipes. Instead they are matched by **tag and property filters**:

- An item definition has `tags` (e.g. `["tool", "mining"]`) and `properties` (e.g. `{"type": "pickaxe", "material": "iron"}`). Properties are automatically merged into tags as `key:value` strings at load time.
- Actions require `requiredItemTags` (e.g. `["mining"]`) rather than a specific item ID. Any item carrying that tag satisfies the requirement.
- Recipe inputs can use `itemFilter` (e.g. `["fuel"]`) to accept any matching item — a smelter recipe that needs fuel will accept both coal and charcoal interchangeably.
- GOAP facts follow the same principle: `has.item-tag.mining` is true whenever the agent holds any mining tool, regardless of its specific ID.

This means adding a new `bronze-pickaxe` with the `mining` tag automatically makes it valid for mining actions and all related GOAP planning without any code changes.

### Production graph (key chains)

```
iron-ore + fuel(any) ──[smelter]──► iron-ingot
tin-ore  + fuel(any) ──[smelter]──► tin-ingot
copper-ore + fuel      ──[smelter]──► copper-ingot
copper-ingot + tin-ingot ──[smelter]──► bronze-ingot

oak-log ──[sawmill]──► oak-planks (×4)
oak-planks ──[sawmill]──► wooden-handle

iron-ingot ──[forge]──► iron-pickaxe-head | iron-axe-head
iron-pickaxe-head + wooden-handle ──[crafting-workshop]──► iron-pickaxe
iron-axe-head    + wooden-handle ──[crafting-workshop]──► iron-axe

oak-log (×3) ──[kiln]──► charcoal (×2)
grain (×3) + fuel(any) ──[cooking-hearth]*──► ration (×4)
grain (×4) + fuel(any) ──[brewery]──► ale (×4)
```

\*requires `recipe.ration-broth` learned fact (from `cookbook` item)

---

## GOAP glossary

The simulation uses a Goal-Oriented Action Planning (GOAP) model for agent decision-making. These are the core terms used in the code and UI.

- Fact: A world-state statement represented as a string, such as `enemy.visible`, `inventory.has-space`, or `self.hidden`.
- Current facts: The set of facts that are true for an agent right now, based on its body state, inventory, memory, nearby threats, and other world queries.
- Goal: A desired outcome the planner tries to satisfy for an agent.
- Desired facts: Facts that must be true for a goal to count as achieved.
- Forbidden facts: Facts that must be false for a goal to count as achieved.
- Required facts: Facts that must already be true before a goal is even eligible to plan for.
- Blocked-by facts: Facts that prevent a goal from being considered when they are currently true.
- Action definition: An authored action template that describes what an action needs, what it changes, and what kind of target it can operate on.
- Candidate: A concrete action option materialized from an action definition for a specific world state. A candidate is not just "mine" or "sleep" in the abstract; it is that action bound to a specific target, destination, cost, and resolved fact changes for the current planning state.
- Candidate plan: A planner output. It is a complete plan tree from the current world state toward a goal, with requirement nodes and action leaves that can later be flattened into a linear execution path.
- Candidate query: The step where the world-query layer materializes concrete candidates for a specific planning state so the planner can choose among real targetable actions.
- Planner: The component that accepts world state as input, recursively walks the goal/action/fact state space, and outputs candidate plans.
- Plan selection: A later step outside the planner where the agent or another runtime policy chooses which candidate plan to execute.
- Plan: A hierarchical tree of requirements and action leaves that can be flattened into the execution order consumed by the runtime.
- Step: A single candidate inside a plan.
- Cost: The numeric planning cost used to compare alternative plans. Lower-cost plans are preferred.

## Planning architecture

The planner is tree-first, and candidate plans are outputs, not inputs.

1. It starts from the current world facts and a goal's desired or forbidden facts.
2. It selects an action definition that can satisfy the unresolved fact.
3. It recursively resolves that action's prerequisites as child requirement nodes.
4. Once the prerequisites are satisfied, it materializes concrete candidates for that resolved planning state.
5. It emits one or more candidate plans as trees whose leaves describe the executable path through the world state.
6. A later runtime selection step can choose which candidate plan to execute, optionally using agent preference or other policy.

This means the planner owns plan construction. The world-query layer does not choose plans; it only answers "what concrete actions exist for this state?" when the planner asks. The agent runtime may still prefer one candidate plan over another, but that preference is not part of the planner contract.

### Plan tree terms

- Goal node: The root of the plan for a specific goal.
- Requirement node: A dependency that must be satisfied before its parent action or goal can complete.
- Action node: A leaf node containing a concrete action candidate.
- Flattening: Traversing the plan tree left-to-right to turn action leaves into the linear execution queue.

### Candidate example

An action definition might say that `sleep` can target a bed and adds a restored/rested outcome. A candidate is the concrete option produced from that definition for the resolved planning state, such as "sleep in bed at cell (12, 4), path to adjacent cell (11, 4), total cost 8". The planner does not receive candidate plans as input. It resolves the prerequisite tree, asks the world-query layer to materialize real action candidates for each resolved state, and produces candidate plans as output. Those plan trees can then be traversed left-to-right across their leaf actions to create the final execution sequence.
