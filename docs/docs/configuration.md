# Configuration

The configurations values do exactly what they are labeled, so I believe it doesn't need further explanation.

#### deathrun.json(default)
```json
{
  "GiveWeaponToCTs": true,
  "RemoveBuyZones": true,
  "RemoveMoneyFromGameAndHud": true,
  "SetRoundTimeOneHour": true,
  "EnableClippingThroughTeamMates": true,
  "EnableAutoBunnyHopping": true,
  "EnableKillCommandForCTs": true,
  "EnableKillCommandForTs": false,
  "Prefix": "{GREEN}[Deathrun]{DEFAULT}"
}
```

#### lives_system.json(default)
```json
{
  "EnableLivesSystem": false,
  "StartLivesNum": 1,
  "ShowLivesCounter": true,
  "SaveLivesToDatabase": false,
  "Spacer": "// If SaveLivesToDatabase is true, you have to configure the database connection details below too.",
  "Host": "localhost",
  "Database": "database_name",
  "User": "database_user",
  "Password": "database_password",
  "Port": 3306,
  "TableName": "deathrun_players"
}
```

#### game_cvars.json(default)
```json
{
  "Cash": [
    //commands from this block are only executed if config var RemoveMoneyFromGameAndHud = true
  ],
  "Teams": [
    "mp_limitteams 0",
    "mp_autoteambalance false",
    "mp_autokick 0",
    "bot_quota_mode fill",
    "bot_join_team ct",
    "mp_ct_default_melee weapon_knife",
    "mp_ct_default_secondary weapon_usp_silencer",
    "mp_ct_default_primary",
    "mp_t_default_melee weapon_knife",
    "mp_t_default_secondary",
    "mp_t_default_primary",
    "sv_alltalk 0"
  ],
  "Movement": [
    "sv_enablebunnyhopping 1",
    "sv_airaccelerate 99999",
    "sv_wateraccelerate 50",
    "sv_accelerate_use_weapon_speed 0",
    "sv_maxspeed 9999",
    "sv_stopspeed 0",
    "sv_backspeed 0.1",
    "sv_accelerate 50",
    "sv_staminamax 0",
    "sv_maxvelocity 9000",
    "sv_staminajumpcost 0",
    "sv_staminalandcost 0",
    "sv_staminarecoveryrate 0"
  ],
  "RoundTimer": [
    //commands from this block are only executed if config var SetRoundTimeOneHour = true
  ],
  "PlayerClipping": [
    //commands from this block are only executed if config var EnableClippingThroughTeamMates = true
  ]
}
```
