package com.alsphalt.apkgame;

import android.content.Context;
import android.content.SharedPreferences;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.LinearGradient;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.RectF;
import android.graphics.Shader;
import android.os.SystemClock;
import android.view.MotionEvent;
import android.view.View;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.List;
import java.util.Random;

/**
 * Endless highway runner. Pure Canvas rendering, no third-party dependencies, so the
 * whole game builds with nothing but the Android Gradle plugin.
 */
public class GameView extends View {

    private static final int READY = 0;
    private static final int PLAYING = 1;
    private static final int OVER = 2;

    private static final int LANES = 3;

    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint textPaint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Path path = new Path();
    private final RectF rect = new RectF();
    private final Random random = new Random();
    private final List<Obstacle> obstacles = new ArrayList<>();

    private final SharedPreferences prefs;

    private float w, h;
    private float horizonY, bottomY;

    private int state = READY;
    private float score = 0f;
    private int best = 0;
    private float speed = 0.5f;
    private float distance = 0f;
    private float spawnTimer = 1.2f;
    private float dashPhase = 0f;
    private float bend = 0f;

    private float playerOff = 0f;      // lateral offset in px from road centre at the bottom
    private float targetOff = 0f;
    private float shake = 0f;

    private long lastFrame = 0L;
    private boolean attached = false;

    private int[] obstacleColors = {
            Color.rgb(232, 232, 236), Color.rgb(38, 40, 46), Color.rgb(150, 40, 40),
            Color.rgb(48, 78, 150), Color.rgb(120, 124, 130), Color.rgb(196, 168, 60)
    };

    public GameView(Context context) {
        super(context);
        prefs = context.getSharedPreferences("apk_game", Context.MODE_PRIVATE);
        best = prefs.getInt("best", 0);
        setBackgroundColor(Color.BLACK);
        textPaint.setColor(Color.WHITE);
        textPaint.setFakeBoldText(true);
    }

    // ---------- lifecycle ----------

    public void resumeGame() {
        attached = true;
        lastFrame = SystemClock.uptimeMillis();
        invalidate();
    }

    public void pauseGame() {
        attached = false;
    }

    @Override
    protected void onSizeChanged(int nw, int nh, int oldw, int oldh) {
        super.onSizeChanged(nw, nh, oldw, oldh);
        w = nw;
        h = nh;
        horizonY = h * 0.40f;
        bottomY = h * 1.05f;
    }

    // ---------- geometry ----------

    private float centerAt(float t) {
        float k = 1f - t;
        return w * 0.5f + bend * k * k;
    }

    private float halfAt(float t) {
        return w * (0.012f + 0.47f * t * t);
    }

    private float yAt(float t) {
        return horizonY + (bottomY - horizonY) * t * t;
    }

    private float laneX(int lane, float t) {
        return centerAt(t) + (lane - 1) * halfAt(t) * 0.62f;
    }

    private float laneWidth(float t) {
        return halfAt(t) * 0.62f;
    }

    // ---------- game loop ----------

    private void step(float dt) {
        if (state == PLAYING) {
            distance += dt * speed * 420f;
            score += dt * speed * 90f;
            speed = Math.min(1.55f, 0.5f + score * 0.00055f);
            dashPhase += dt * speed * 1.9f;
            bend = (float) Math.sin(distance * 0.00035) * w * 0.16f;
            shake = Math.max(0f, shake - dt * 3.5f);

            playerOff += (targetOff - playerOff) * Math.min(1f, dt * 12f);
            float limit = halfAt(1f) * 0.86f;
            if (playerOff > limit) playerOff = limit;
            if (playerOff < -limit) playerOff = -limit;

            spawnTimer -= dt;
            if (spawnTimer <= 0f) {
                spawn();
                float gap = Math.max(0.42f, 1.15f - score * 0.00035f);
                spawnTimer = gap + random.nextFloat() * 0.35f;
            }

            for (int i = obstacles.size() - 1; i >= 0; i--) {
                Obstacle o = obstacles.get(i);
                o.t += dt * speed * o.relSpeed;
                if (o.t > 1.12f) {
                    obstacles.remove(i);
                    continue;
                }
                if (!o.counted && o.t > 1.0f) {
                    o.counted = true;
                }
                if (o.t > 0.86f && o.t < 1.06f && !o.hit) {
                    float dx = Math.abs(laneX(o.lane, 1f) + o.drift - (centerAt(1f) + playerOff));
                    float need = (laneWidth(1f) * 0.52f + laneWidth(1f) * 0.52f) * 0.78f;
                    if (dx < need) {
                        o.hit = true;
                        crash();
                    }
                }
            }
        } else {
            dashPhase += dt * 0.22f;
            bend = (float) Math.sin(distance * 0.00035 + SystemClock.uptimeMillis() * 0.0002) * w * 0.05f;
        }
    }

    private void spawn() {
        boolean[] used = new boolean[LANES];
        int free = 0;
        for (Obstacle o : obstacles) {
            if (o.t < 0.22f) {
                used[o.lane] = true;
            }
        }
        for (int i = 0; i < LANES; i++) if (!used[i]) free++;
        if (free <= 1) return;

        int lane = random.nextInt(LANES);
        if (used[lane]) {
            for (int i = 0; i < LANES; i++) {
                int probe = (lane + i) % LANES;
                if (!used[probe]) { lane = probe; break; }
            }
        }

        Obstacle o = new Obstacle();
        o.lane = lane;
        o.t = -0.02f;
        o.color = obstacleColors[random.nextInt(obstacleColors.length)];
        o.relSpeed = 0.55f + random.nextFloat() * 0.2f;
        o.drift = (random.nextFloat() - 0.5f) * w * 0.012f;
        obstacles.add(o);
    }

    private void crash() {
        state = OVER;
        shake = 1f;
        int s = (int) score;
        if (s > best) {
            best = s;
            prefs.edit().putInt("best", best).apply();
        }
    }

    private void startRun() {
        state = PLAYING;
        score = 0f;
        speed = 0.5f;
        distance = 0f;
        spawnTimer = 1.0f;
        obstacles.clear();
        playerOff = 0f;
        targetOff = 0f;
        shake = 0f;
    }

    // ---------- drawing ----------

    @Override
    protected void onDraw(Canvas canvas) {
        super.onDraw(canvas);
        if (w <= 0f || h <= 0f) return;

        long now = SystemClock.uptimeMillis();
        float dt = lastFrame == 0L ? 0.016f : (now - lastFrame) / 1000f;
        lastFrame = now;
        if (dt > 0.06f) dt = 0.06f;

        step(dt);

        canvas.save();
        if (shake > 0.01f) {
            float m = shake * h * 0.012f;
            canvas.translate((random.nextFloat() - 0.5f) * m, (random.nextFloat() - 0.5f) * m);
        }

        drawSky(canvas);
        drawGround(canvas);
        drawRoad(canvas);
        drawObstacles(canvas);
        drawPlayer(canvas);
        drawHud(canvas);

        canvas.restore();

        if (attached) postInvalidateOnAnimation();
    }

    private void drawSky(Canvas canvas) {
        paint.setStyle(Paint.Style.FILL);
        paint.setShader(new LinearGradient(0f, 0f, 0f, horizonY,
                Color.rgb(16, 24, 44), Color.rgb(92, 132, 178), Shader.TileMode.CLAMP));
        canvas.drawRect(0f, 0f, w, horizonY, paint);
        paint.setShader(null);

        paint.setColor(Color.argb(90, 255, 236, 200));
        canvas.drawCircle(w * 0.74f, horizonY * 0.42f, h * 0.055f, paint);

        paint.setColor(Color.rgb(28, 42, 60));
        float baseY = horizonY;
        path.reset();
        path.moveTo(0f, baseY);
        int blocks = 14;
        for (int i = 0; i <= blocks; i++) {
            float x = w * i / blocks;
            float bh = h * (0.03f + 0.075f * ((i * 37 % 11) / 11f));
            path.lineTo(x, baseY - bh);
            path.lineTo(x + w / blocks * 0.55f, baseY - bh);
        }
        path.lineTo(w, baseY);
        path.close();
        canvas.drawPath(path, paint);
    }

    private void drawGround(Canvas canvas) {
        paint.setShader(null);
        paint.setColor(Color.rgb(30, 40, 32));
        canvas.drawRect(0f, horizonY, w, h, paint);
    }

    private void drawRoad(Canvas canvas) {
        paint.setShader(null);
        paint.setStyle(Paint.Style.FILL);

        // shoulder
        paint.setColor(Color.rgb(66, 70, 78));
        path.reset();
        path.moveTo(centerAt(1f) - halfAt(1f) * 1.18f, bottomY);
        path.lineTo(centerAt(0f) - halfAt(0f) * 1.35f, horizonY);
        path.lineTo(centerAt(0f) + halfAt(0f) * 1.35f, horizonY);
        path.lineTo(centerAt(1f) + halfAt(1f) * 1.18f, bottomY);
        path.close();
        canvas.drawPath(path, paint);

        // asphalt
        paint.setColor(Color.rgb(42, 46, 53));
        path.reset();
        path.moveTo(centerAt(1f) - halfAt(1f), bottomY);
        path.lineTo(centerAt(0f) - halfAt(0f), horizonY);
        path.lineTo(centerAt(0f) + halfAt(0f), horizonY);
        path.lineTo(centerAt(1f) + halfAt(1f), bottomY);
        path.close();
        canvas.drawPath(path, paint);

        // edge lines
        paint.setColor(Color.rgb(226, 226, 226));
        for (int side = -1; side <= 1; side += 2) {
            path.reset();
            path.moveTo(centerAt(1f) + side * halfAt(1f) * 0.97f, bottomY);
            for (int i = 10; i >= 0; i--) {
                float t = i / 10f;
                path.lineTo(centerAt(t) + side * halfAt(t) * 0.97f, yAt(t));
            }
            paint.setStyle(Paint.Style.STROKE);
            paint.setStrokeWidth(h * 0.006f);
            canvas.drawPath(path, paint);
        }
        paint.setStyle(Paint.Style.FILL);

        // lane dashes
        paint.setColor(Color.rgb(232, 232, 232));
        paint.setStrokeCap(Paint.Cap.BUTT);
        for (int divider = 0; divider < 2; divider++) {
            float off = divider == 0 ? -0.31f : 0.31f;
            for (int k = 0; k < 9; k++) {
                float t0 = frac(k / 9f + dashPhase);
                float t1 = frac(t0 + 0.055f);
                if (t1 <= t0 || t0 < 0.02f) continue;
                float x0 = centerAt(t0) + off * 2f * halfAt(t0);
                float x1 = centerAt(t1) + off * 2f * halfAt(t1);
                paint.setStrokeWidth(Math.max(1.5f, halfAt(t1) * 0.035f));
                canvas.drawLine(x0, yAt(t0), x1, yAt(t1), paint);
            }
        }
    }

    private static float frac(float v) {
        return v - (float) Math.floor(v);
    }

    private void drawObstacles(Canvas canvas) {
        Collections.sort(obstacles, new Comparator<Obstacle>() {
            @Override
            public int compare(Obstacle a, Obstacle b) {
                return Float.compare(a.t, b.t);
            }
        });

        for (Obstacle o : obstacles) {
            if (o.t < 0f) continue;
            float t = Math.min(o.t, 1.1f);
            float cw = laneWidth(t) * 0.86f;
            float ch = cw * (1.35f + 0.9f * t);
            float cx = laneX(o.lane, t) + o.drift * t;
            float cy = yAt(t);
            if (cw < 2f) continue;
            drawCar(canvas, cx, cy, cw, ch, o.color, -1);
        }
    }

    private void drawPlayer(Canvas canvas) {
        float cw = laneWidth(1f) * 0.9f;
        float ch = cw * 2.1f;
        float cx = centerAt(1f) + playerOff;
        float cy = bottomY - ch * 0.62f;
        drawCar(canvas, cx, cy, cw, ch, Color.rgb(206, 42, 38), 1);
    }

    /**
     * @param facing 1 for the player (seen from behind), -1 for oncoming traffic.
     */
    private void drawCar(Canvas canvas, float cx, float cy, float cw, float ch, int color, int facing) {
        paint.setShader(null);
        paint.setStyle(Paint.Style.FILL);

        // shadow
        paint.setColor(Color.argb(90, 0, 0, 0));
        rect.set(cx - cw * 0.56f, cy + ch * 0.34f, cx + cw * 0.56f, cy + ch * 0.52f);
        canvas.drawRoundRect(rect, cw * 0.2f, cw * 0.2f, paint);

        // body
        paint.setColor(color);
        rect.set(cx - cw * 0.5f, cy - ch * 0.5f, cx + cw * 0.5f, cy + ch * 0.5f);
        canvas.drawRoundRect(rect, cw * 0.18f, cw * 0.18f, paint);

        // cabin
        paint.setColor(shade(color, 0.75f));
        if (facing > 0) {
            rect.set(cx - cw * 0.38f, cy - ch * 0.34f, cx + cw * 0.38f, cy + ch * 0.12f);
        } else {
            rect.set(cx - cw * 0.38f, cy - ch * 0.20f, cx + cw * 0.38f, cy + ch * 0.26f);
        }
        canvas.drawRoundRect(rect, cw * 0.12f, cw * 0.12f, paint);

        // windscreen
        paint.setColor(Color.rgb(28, 38, 52));
        if (facing > 0) {
            rect.set(cx - cw * 0.32f, cy - ch * 0.28f, cx + cw * 0.32f, cy - ch * 0.06f);
        } else {
            rect.set(cx - cw * 0.32f, cy - ch * 0.12f, cx + cw * 0.32f, cy + ch * 0.10f);
        }
        canvas.drawRoundRect(rect, cw * 0.08f, cw * 0.08f, paint);

        // lights
        if (facing > 0) {
            paint.setColor(Color.rgb(255, 74, 60));
            canvas.drawRect(cx - cw * 0.42f, cy + ch * 0.30f, cx - cw * 0.18f, cy + ch * 0.40f, paint);
            canvas.drawRect(cx + cw * 0.18f, cy + ch * 0.30f, cx + cw * 0.42f, cy + ch * 0.40f, paint);
        } else {
            paint.setColor(Color.rgb(255, 244, 210));
            canvas.drawRect(cx - cw * 0.42f, cy + ch * 0.26f, cx - cw * 0.20f, cy + ch * 0.36f, paint);
            canvas.drawRect(cx + cw * 0.20f, cy + ch * 0.26f, cx + cw * 0.42f, cy + ch * 0.36f, paint);
        }
    }

    private static int shade(int color, float factor) {
        int r = (int) (Color.red(color) * factor);
        int g = (int) (Color.green(color) * factor);
        int b = (int) (Color.blue(color) * factor);
        return Color.rgb(Math.min(255, r), Math.min(255, g), Math.min(255, b));
    }

    private void drawHud(Canvas canvas) {
        textPaint.setColor(Color.WHITE);
        textPaint.setTextAlign(Paint.Align.LEFT);
        textPaint.setTextSize(h * 0.062f);
        canvas.drawText(String.valueOf((int) score), w * 0.035f, h * 0.11f, textPaint);

        textPaint.setTextSize(h * 0.032f);
        textPaint.setColor(Color.argb(190, 255, 255, 255));
        canvas.drawText("BEST " + best, w * 0.035f, h * 0.17f, textPaint);

        textPaint.setTextAlign(Paint.Align.RIGHT);
        canvas.drawText(Math.round(speed * 190f) + " KM/H", w * 0.965f, h * 0.11f, textPaint);

        if (state != PLAYING) {
            textPaint.setTextAlign(Paint.Align.CENTER);

            paint.setColor(Color.argb(150, 0, 0, 0));
            canvas.drawRect(0f, h * 0.30f, w, h * 0.72f, paint);

            textPaint.setColor(Color.WHITE);
            textPaint.setTextSize(h * 0.085f);
            canvas.drawText(state == READY ? "DARK NOTE" : "CRASHED", w * 0.5f, h * 0.43f, textPaint);

            textPaint.setTextSize(h * 0.040f);
            textPaint.setColor(Color.argb(220, 255, 255, 255));
            if (state == OVER) {
                canvas.drawText("SCORE " + (int) score + "   BEST " + best, w * 0.5f, h * 0.52f, textPaint);
                canvas.drawText("TAP TO RACE AGAIN", w * 0.5f, h * 0.60f, textPaint);
            } else {
                canvas.drawText("TAP TO START", w * 0.5f, h * 0.52f, textPaint);
                canvas.drawText("DRAG LEFT / RIGHT TO STEER", w * 0.5f, h * 0.60f, textPaint);
            }
        }
    }

    // ---------- input ----------

    @Override
    public boolean onTouchEvent(MotionEvent event) {
        switch (event.getActionMasked()) {
            case MotionEvent.ACTION_DOWN:
                if (state != PLAYING) {
                    startRun();
                    return true;
                }
                updateTarget(event.getX());
                return true;
            case MotionEvent.ACTION_MOVE:
                updateTarget(event.getX());
                return true;
            default:
                return true;
        }
    }

    private void updateTarget(float x) {
        float limit = halfAt(1f) * 0.86f;
        float off = x - centerAt(1f);
        if (off > limit) off = limit;
        if (off < -limit) off = -limit;
        targetOff = off;
    }

    private static class Obstacle {
        float t;
        int lane;
        int color;
        float relSpeed;
        float drift;
        boolean hit;
        boolean counted;
    }
}
