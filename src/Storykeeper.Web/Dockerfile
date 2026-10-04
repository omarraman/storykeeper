FROM node:22-alpine

WORKDIR /app
COPY package.json package-lock.json ./
COPY src/Storykeeper.Web/package.json src/Storykeeper.Web/package.json
RUN npm ci
COPY src/Storykeeper.Web/ src/Storykeeper.Web/

EXPOSE 5173
CMD ["npm", "run", "dev", "--workspace", "@storykeeper/web", "--", "--host", "0.0.0.0"]
