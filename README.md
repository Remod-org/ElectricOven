# ElectricOven
Cauldrons and BBQ can use electricity instead of wood.

When spawning either of these items, an electrical branch and simple light are spawned as part of the device.  For the BBQ, the branch will be on the left side, and for the cauldron it will be on the right.  For the BBQ, the simple light should appear in the middle of the grill while it is operating.

If power is applied and wood is in the inventory, the oven will not start.  However, if power is not applied but wood is present, the oven will operate as normal.

If you set allowOvercooking == true in the configuration, the oven will continue to cook after the food is finished cooking, which will cause it to burn.

Status indication will be shown in the header for the inventory container when opened, e.g. "Electricity: On"

## Permissions

No permissions are required at this time.

## Commands

  - `/cr` - Toggle enabling of the electrical component when placing a BBQ or cauldron (per player).  This will invert the defaultEnabled setting for a player.

## Configuration
```js
{
  "Settings": {
    "defaultEnabled": true,
    "allowOvercooking": false,
    "debug": false
  },
  "Version": {
    "Major": 1,
    "Minor": 0,
    "Patch": 2
  }
}
```

