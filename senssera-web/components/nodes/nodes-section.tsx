"use client";

import { toast } from "sonner";
import { Cpu, KeyRound, Plus, Trash2 } from "lucide-react";
import { useDeleteNode, useNodes, useRotateNodeToken } from "@/hooks/use-nodes";
import { METRIC_META } from "@/lib/metrics";
import { timeAgo } from "@/lib/time";
import type { Node, NodeStatus } from "@/lib/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog";
import { NodeFormDialog } from "@/components/nodes/node-form-dialog";
import { ChannelFormDialog } from "@/components/nodes/channel-form-dialog";

const STATUS_META: Record<NodeStatus, { label: string; dot: string }> = {
  online: { label: "online", dot: "bg-foreground" },
  offline: { label: "offline", dot: "bg-alert" },
  neverSeen: { label: "waiting for first reading", dot: "bg-muted-foreground/50" },
};

export function NodesSection({
  greenhouseId,
  isAdmin,
  onToken,
}: {
  greenhouseId: string;
  isAdmin: boolean;
  onToken: (token: string, name: string) => void;
}) {
  const nodes = useNodes(greenhouseId);

  // Viewers only see this section once a board exists; admins always get the way in.
  if (!isAdmin && nodes.data?.length === 0) return null;

  return (
    <section className="space-y-3">
      <div className="flex items-end justify-between gap-4 border-b border-dashed pt-4 pb-2">
        <div>
          <p className="label-caps">Hardware</p>
          <h2 className="font-serif text-2xl leading-tight">Boards</h2>
        </div>
        {isAdmin && (
          <NodeFormDialog
            greenhouseId={greenhouseId}
            onCreated={(node) => onToken(node.token, node.name)}
            trigger={
              <Button variant="outline" size="sm">
                <Plus className="mr-1 h-4 w-4" />
                Add board
              </Button>
            }
          />
        )}
      </div>

      {nodes.isLoading && <Skeleton className="h-32 w-full rounded-md" />}

      {nodes.isError && (
        <p className="font-serif italic text-alert-text">Boards couldn’t be loaded.</p>
      )}

      {nodes.data && nodes.data.length === 0 && (
        <Card className="items-center p-8 text-center">
          <p className="font-serif text-xl">No boards yet.</p>
          <p className="text-sm text-muted-foreground">
            Register an ESP32 to report several sensors with one token.
          </p>
        </Card>
      )}

      {nodes.data?.map((node) => (
        <NodeCard
          key={node.id}
          node={node}
          greenhouseId={greenhouseId}
          isAdmin={isAdmin}
          onToken={onToken}
        />
      ))}
    </section>
  );
}

function NodeCard({
  node,
  greenhouseId,
  isAdmin,
  onToken,
}: {
  node: Node;
  greenhouseId: string;
  isAdmin: boolean;
  onToken: (token: string, name: string) => void;
}) {
  const status = STATUS_META[node.status];
  const rotate = useRotateNodeToken(greenhouseId);
  const del = useDeleteNode(greenhouseId);

  async function handleRotate() {
    try {
      const result = await rotate.mutateAsync(node.id);
      onToken(result.token, node.name);
    } catch {
      toast.error("Could not rotate the token.");
    }
  }

  async function handleDelete() {
    try {
      await del.mutateAsync(node.id);
      toast.success("Board deleted");
    } catch {
      toast.error("Could not delete the board.");
    }
  }

  return (
    <Card className="gap-0 overflow-hidden py-0">
      <div className="flex flex-wrap items-start justify-between gap-3 px-4 py-3">
        <div className="min-w-0 space-y-1">
          <p className="flex items-center gap-2 font-serif text-xl leading-tight">
            <Cpu className="h-4 w-4 shrink-0 text-muted-foreground" />
            <span className="truncate">{node.name}</span>
          </p>
          <p className="flex flex-wrap items-center gap-x-3 gap-y-1 font-mono text-xs text-muted-foreground">
            <span className="inline-flex items-center gap-1.5 text-foreground">
              <span className={`inline-block h-2 w-2 rounded-full ${status.dot}`} />
              {status.label}
            </span>
            {node.lastSeenAt && <span>seen {timeAgo(node.lastSeenAt)}</span>}
            <span>fw {node.firmwareVersion ?? "—"}</span>
          </p>
        </div>

        {isAdmin && (
          <div className="flex gap-1">
            <ChannelFormDialog
              greenhouseId={greenhouseId}
              nodeId={node.id}
              nodeName={node.name}
              trigger={
                <Button variant="ghost" size="sm">
                  <Plus className="mr-1 h-4 w-4" />
                  Channel
                </Button>
              }
            />
            <AlertDialog>
              <AlertDialogTrigger asChild>
                <Button
                  variant="ghost"
                  size="icon"
                  aria-label="Rotate board token"
                  disabled={rotate.isPending}
                >
                  <KeyRound className="h-4 w-4" />
                </Button>
              </AlertDialogTrigger>
              <AlertDialogContent>
                <AlertDialogHeader>
                  <AlertDialogTitle>Rotate the token for “{node.name}”?</AlertDialogTitle>
                  <AlertDialogDescription>
                    The current token stops working immediately. The board
                    can&apos;t send readings until the new token is entered on it.
                  </AlertDialogDescription>
                </AlertDialogHeader>
                <AlertDialogFooter>
                  <AlertDialogCancel>Cancel</AlertDialogCancel>
                  <AlertDialogAction onClick={handleRotate}>Rotate token</AlertDialogAction>
                </AlertDialogFooter>
              </AlertDialogContent>
            </AlertDialog>
            <AlertDialog>
              <AlertDialogTrigger asChild>
                <Button
                  variant="ghost"
                  size="icon"
                  aria-label="Delete board"
                  className="text-muted-foreground hover:text-destructive"
                >
                  <Trash2 className="h-4 w-4" />
                </Button>
              </AlertDialogTrigger>
              <AlertDialogContent>
                <AlertDialogHeader>
                  <AlertDialogTitle>Delete “{node.name}”?</AlertDialogTitle>
                  <AlertDialogDescription>
                    This removes the board and all {node.channels.length} of its
                    channels, with their readings and alert history. This cannot
                    be undone.
                  </AlertDialogDescription>
                </AlertDialogHeader>
                <AlertDialogFooter>
                  <AlertDialogCancel>Cancel</AlertDialogCancel>
                  <AlertDialogAction
                    onClick={handleDelete}
                    className="bg-destructive text-white hover:bg-destructive/90"
                  >
                    Delete
                  </AlertDialogAction>
                </AlertDialogFooter>
              </AlertDialogContent>
            </AlertDialog>
          </div>
        )}
      </div>

      {node.channels.length === 0 ? (
        <p className="border-t border-dashed px-4 py-3 text-sm text-muted-foreground">
          No channels yet{isAdmin ? " — add one for each sensor wired to the board." : "."}
        </p>
      ) : (
        <div className="border-t">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Channel</TableHead>
                <TableHead>Name</TableHead>
                <TableHead>Metric</TableHead>
                <TableHead className="text-right">Last reading</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {node.channels.map((c) => {
                const meta = METRIC_META[c.metric];
                const Icon = meta.icon;
                return (
                  <TableRow key={c.deviceId}>
                    <TableCell className="font-mono text-xs">{c.channel}</TableCell>
                    <TableCell className="font-medium">{c.name}</TableCell>
                    <TableCell>
                      <Badge variant="secondary" className="gap-1 font-normal">
                        <Icon className="h-3.5 w-3.5" />
                        {meta.label}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-right font-mono text-xs text-muted-foreground">
                      {timeAgo(c.lastSeenAt)}
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </div>
      )}
    </Card>
  );
}
