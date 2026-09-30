"use client";

import { useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { toast } from "sonner";
import {
  ArrowLeft,
  Plus,
  KeyRound,
  Trash2,
} from "lucide-react";
import { useAuth } from "@/lib/auth";
import { useGreenhouse } from "@/hooks/use-greenhouses";
import { useDevices, useDeleteDevice, useRotateToken } from "@/hooks/use-devices";
import { METRIC_META } from "@/lib/metrics";
import type { Device, DeviceWithToken } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
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
import { DeviceFormDialog } from "@/components/devices/device-form-dialog";
import { DeviceTokenDialog } from "@/components/devices/device-token-dialog";
import { GreenhouseMonitoring } from "@/components/greenhouses/greenhouse-monitoring";
import { ThresholdsSection } from "@/components/thresholds/thresholds-section";
import { SpecimenHeader } from "@/components/notebook/specimen-header";

const DAY_MS = 86_400_000;

export default function GreenhouseDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { user } = useAuth();
  const isAdmin = user?.role === "Admin";

  const greenhouse = useGreenhouse(id);
  // Captured once per visit; the day count doesn't need to tick while the page is open.
  const [openedAt] = useState(() => Date.now());
  const devices = useDevices(id);

  const [reveal, setReveal] = useState<{ token: string; name: string } | null>(
    null,
  );

  function onDeviceCreated(device: DeviceWithToken) {
    setReveal({ token: device.token, name: device.name });
  }

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div>
        <Link
          href="/greenhouses"
          className="inline-flex items-center text-sm text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="mr-1 h-4 w-4" />
          <span className="font-serif italic">All greenhouses</span>
        </Link>
      </div>

      {greenhouse.data ? (
        <SpecimenHeader
          code={greenhouse.data.location ? `GH · ${greenhouse.data.location}` : "GH · no location"}
          subtitle={`day ${Math.floor((openedAt - Date.parse(greenhouse.data.createdAt)) / DAY_MS) + 1}`}
          title={greenhouse.data.name}
        />
      ) : (
        <Skeleton className="h-16 w-full" />
      )}

      <GreenhouseMonitoring greenhouseId={id} />

      <div className="flex items-end justify-between gap-4 border-b border-dashed pt-4 pb-2">
        <div>
          <p className="label-caps">Instruments</p>
          <h2 className="font-serif text-2xl leading-tight">Devices</h2>
        </div>
        {isAdmin && (
          <DeviceFormDialog
            greenhouseId={id}
            onCreated={onDeviceCreated}
            trigger={
              <Button>
                <Plus className="mr-1 h-4 w-4" />
                Add device
              </Button>
            }
          />
        )}
      </div>

      {devices.isLoading && <Skeleton className="h-56 w-full rounded-md" />}

      {devices.isError && (
        <p className="font-serif italic text-alert-text">Devices couldn’t be loaded. Refresh to try again.</p>
      )}

      {devices.data && devices.data.length === 0 && (
        <Card className="items-center gap-3 p-10 text-center">
          <p className="font-serif text-2xl">No instruments yet.</p>
          <p className="text-sm text-muted-foreground">Add a device to start recording readings.</p>
          {isAdmin && (
            <DeviceFormDialog
              greenhouseId={id}
              onCreated={onDeviceCreated}
              trigger={
                <Button>
                  <Plus className="mr-1 h-4 w-4" />
                  Add device
                </Button>
              }
            />
          )}
        </Card>
      )}

      {devices.data && devices.data.length > 0 && (
        <Card className="overflow-hidden py-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Metric</TableHead>
                <TableHead>Status</TableHead>
                {isAdmin && <TableHead className="text-right">Actions</TableHead>}
              </TableRow>
            </TableHeader>
            <TableBody>
              {devices.data.map((device) => (
                <DeviceRow
                  key={device.id}
                  device={device}
                  greenhouseId={id}
                  isAdmin={isAdmin}
                  onToken={(token) =>
                    setReveal({ token, name: device.name })
                  }
                />
              ))}
            </TableBody>
          </Table>
        </Card>
      )}

      {isAdmin && <ThresholdsSection greenhouseId={id} />}

      <DeviceTokenDialog
        token={reveal?.token ?? null}
        deviceName={reveal?.name}
        onClose={() => setReveal(null)}
      />
    </div>
  );
}

function DeviceRow({
  device,
  greenhouseId,
  isAdmin,
  onToken,
}: {
  device: Device;
  greenhouseId: string;
  isAdmin: boolean;
  onToken: (token: string) => void;
}) {
  const meta = METRIC_META[device.metric];
  const MetricIcon = meta.icon;
  const rotate = useRotateToken(greenhouseId);
  const del = useDeleteDevice(greenhouseId);

  async function handleRotate() {
    try {
      const result = await rotate.mutateAsync(device.id);
      onToken(result.token);
    } catch {
      toast.error("Could not rotate the token.");
    }
  }

  async function handleDelete() {
    try {
      await del.mutateAsync(device.id);
      toast.success("Device deleted");
    } catch {
      toast.error("Could not delete the device.");
    }
  }

  return (
    <TableRow>
      <TableCell className="font-medium">{device.name}</TableCell>
      <TableCell>
        <Badge variant="secondary" className="gap-1 font-normal">
          <MetricIcon className="h-3.5 w-3.5" />
          {meta.label}
        </Badge>
      </TableCell>
      <TableCell>
        <span className="inline-flex items-center gap-1.5 font-mono text-xs">
          <span
            className={
              device.status === "Active"
                ? "inline-block h-2 w-2 rounded-full bg-foreground"
                : "inline-block h-2 w-2 rounded-full bg-muted-foreground/50"
            }
          />
          {device.status}
        </span>
      </TableCell>
      {isAdmin && (
        <TableCell className="text-right">
          <div className="flex justify-end gap-1">
            <Button
              variant="ghost"
              size="icon"
              aria-label="Rotate token"
              onClick={handleRotate}
              disabled={rotate.isPending}
            >
              <KeyRound className="h-4 w-4" />
            </Button>
            <AlertDialog>
              <AlertDialogTrigger asChild>
                <Button
                  variant="ghost"
                  size="icon"
                  aria-label="Delete device"
                  className="text-muted-foreground hover:text-destructive"
                >
                  <Trash2 className="h-4 w-4" />
                </Button>
              </AlertDialogTrigger>
              <AlertDialogContent>
                <AlertDialogHeader>
                  <AlertDialogTitle>Delete “{device.name}”?</AlertDialogTitle>
                  <AlertDialogDescription>
                    This removes the device and its readings. This cannot be
                    undone.
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
        </TableCell>
      )}
    </TableRow>
  );
}
