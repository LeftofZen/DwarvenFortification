# DwarvenFortification

A little simulation inspired by Dwarf Fortress

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
