FROM node:24-alpine AS build
WORKDIR /app

COPY frontend/StoreControl.Web/package.json frontend/StoreControl.Web/package-lock.json ./
RUN npm ci

COPY frontend/StoreControl.Web .
RUN npm run build

FROM nginx:1.29-alpine AS runtime
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
