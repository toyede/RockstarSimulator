-- Staging first. No operation in this migration touches the ranking table.
begin;
create schema if not exists playtest_private;
revoke all on schema playtest_private from public, anon, authenticated;
create table if not exists playtest_private.samples (
    id uuid primary key,
    received_at timestamptz not null default now(),
    kind text not null check (kind in ('tutorial', 'performance')),
    stage text not null check (stage in ('stage_01','stage_02','stage_03','stage_04','stage_05_boss')),
    outcome text not null,
    chill int not null check (chill between 0 and 3000),
    singalong int not null check (singalong between 0 and 3000),
    mosh int not null check (mosh between 0 and 3000),
    request_specials int not null check (request_specials between 0 and 3000),
    aimed_specials int not null check (aimed_specials between 0 and request_specials),
    hits int not null check (hits between 0 and aimed_specials),
    opening_plays int not null check (opening_plays between 0 and 3),
    opening_positive int not null check (opening_positive between 0 and opening_plays),
    blocked int not null check (blocked between 0 and 3000),
    duration_seconds int not null check (duration_seconds between 0 and 3600),
    check ((kind = 'tutorial' and outcome in ('completed','skipped','abandoned'))
        or (kind = 'performance' and outcome in ('cleared','failed','abandoned')))
);
alter table playtest_private.samples enable row level security;
revoke all on table playtest_private.samples from public, anon, authenticated;

create or replace function public.record_playtest_sample(p_sample jsonb)
returns void
language plpgsql
security definer
set search_path = ''
as $$
begin
    if p_sample is null or pg_catalog.jsonb_typeof(p_sample) <> 'object'
       or pg_catalog.pg_column_size(p_sample) > 1024 then
        raise exception 'Invalid sample';
    end if;
    insert into playtest_private.samples
      (id, kind, stage, outcome, chill, singalong, mosh, request_specials,
       aimed_specials, hits, opening_plays, opening_positive, blocked, duration_seconds)
    values (
      (p_sample->>'id')::uuid, p_sample->>'kind', p_sample->>'stage', p_sample->>'outcome',
      (p_sample->>'chill')::int, (p_sample->>'singalong')::int, (p_sample->>'mosh')::int,
      (p_sample->>'requestSpecials')::int, (p_sample->>'aimedSpecials')::int, (p_sample->>'hits')::int,
      (p_sample->>'openingPlays')::int, (p_sample->>'openingPositive')::int, (p_sample->>'blocked')::int,
      (p_sample->>'durationSeconds')::int)
    on conflict (id) do nothing;
end;
$$;
revoke all on function public.record_playtest_sample(jsonb) from public, anon, authenticated;
-- Enable only in the selected test environment once payload and permissions are verified:
-- grant execute on function public.record_playtest_sample(jsonb) to anon;
commit;

-- Dashboard SQL (owner only): sum counts before dividing so rates are weighted.
-- select stage, sum(chill) as chill, sum(singalong) as singalong, sum(mosh) as mosh,
--   sum(hits)::numeric/nullif(sum(aimed_specials), 0) as hit_rate_when_aimed,
--   sum(hits)::numeric/nullif(sum(request_specials), 0) as hit_rate_per_special_play,
--   sum(opening_positive)::numeric/nullif(sum(opening_plays), 0) as early_positive_rate
-- from playtest_private.samples where kind = 'performance' group by stage;
-- select stage, outcome, count(*), avg(duration_seconds), avg(blocked)
-- from playtest_private.samples where kind = 'tutorial' group by stage, outcome;
