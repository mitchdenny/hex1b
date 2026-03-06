/**
 * A simple task manager demonstrating TypeScript syntax.
 */
interface Task {
    id: number;
    title: string;
    completed: boolean;
    priority: "low" | "medium" | "high";
    tags: string[];
}

class TaskManager {
    private tasks: Map<number, Task> = new Map();
    private nextId = 1;

    /**
     * Creates a new task and returns its ID.
     */
    add(title: string, priority: Task["priority"] = "medium"): number {
        const id = this.nextId++;
        const task: Task = {
            id,
            title,
            completed: false,
            priority,
            tags: [],
        };
        this.tasks.set(id, task);
        return id;
    }

    /**
     * Marks a task as completed.
     */
    complete(id: number): boolean {
        const task = this.tasks.get(id);
        if (!task) return false;
        task.completed = true;
        return true;
    }

    /**
     * Gets all pending tasks, optionally filtered by priority.
     */
    getPending(priority?: Task["priority"]): Task[] {
        const result: Task[] = [];
        for (const task of this.tasks.values()) {
            if (!task.completed) {
                if (!priority || task.priority === priority) {
                    result.push(task);
                }
            }
        }
        return result;
    }

    /**
     * Adds a tag to a task.
     */
    addTag(id: number, tag: string): void {
        const task = this.tasks.get(id);
        if (task && !task.tags.includes(tag)) {
            task.tags.push(tag);
        }
    }

    /**
     * Returns a summary of tasks by status.
     */
    summary(): { total: number; completed: number; pending: number } {
        let completed = 0;
        let pending = 0;
        for (const task of this.tasks.values()) {
            if (task.completed) completed++;
            else pending++;
        }
        return { total: this.tasks.size, completed, pending };
    }
}

// Usage example
const manager = new TaskManager();
manager.add("Write documentation", "high");
manager.add("Fix bug #42", "medium");
manager.add("Update dependencies", "low");

manager.complete(1);
manager.addTag(2, "bugfix");

const stats = manager.summary();
console.log(`Tasks: ${stats.total} total, ${stats.completed} done, ${stats.pending} pending`);

const urgent = manager.getPending("high");
console.log(`Urgent tasks: ${urgent.map(t => t.title).join(", ")}`);
